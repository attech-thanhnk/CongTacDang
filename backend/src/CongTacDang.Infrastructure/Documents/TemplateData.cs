using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace CongTacDang.Infrastructure.Documents;

/// <summary>Dữ liệu điền template Word; khóa là Tag của Content Control (không phân biệt hoa thường).</summary>
public sealed class TemplateData
{
    /// <summary>Trường đơn. Giá trị null = giữ nguyên chữ mặc định trong template.</summary>
    public Dictionary<string, string?> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, bool> Conditions { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<TemplateData>> Collections { get; } = new(StringComparer.OrdinalIgnoreCase);

    public TemplateData Field(string tag, string? value)
    {
        Fields[tag] = value;
        return this;
    }

    public TemplateData Condition(string tag, bool value)
    {
        Conditions[tag] = value;
        return this;
    }

    public TemplateData Collection(string tag, IEnumerable<TemplateData> items)
    {
        Collections[tag] = new List<TemplateData>(items);
        return this;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class TemplateFieldAttribute : Attribute
{
    public TemplateFieldAttribute(string tag) => Tag = tag;

    public string Tag { get; }
}

/// <summary>Khối lặp <c>repeat:Tag</c>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class TemplateCollectionAttribute : Attribute
{
    public TemplateCollectionAttribute(string tag) => Tag = tag;

    public string Tag { get; }
}

/// <summary>Khối điều kiện <c>if:Tag</c> / <c>ifnot:Tag</c>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class TemplateConditionAttribute : Attribute
{
    public TemplateConditionAttribute(string tag) => Tag = tag;

    public string Tag { get; }
}

public static class TemplateDataBinder
{
    public static TemplateData Bind(object model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var data = new TemplateData();

        foreach (var property in model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var field = property.GetCustomAttribute<TemplateFieldAttribute>();
            if (field != null)
            {
                var value = property.GetValue(model);
                data.Field(field.Tag, value switch
                {
                    null => null,
                    string s => s,
                    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                    _ => value.ToString()
                });
                continue;
            }

            var condition = property.GetCustomAttribute<TemplateConditionAttribute>();
            if (condition != null)
            {
                data.Condition(condition.Tag, property.GetValue(model) is true);
                continue;
            }

            var collection = property.GetCustomAttribute<TemplateCollectionAttribute>();
            if (collection != null)
            {
                var items = new List<TemplateData>();
                if (property.GetValue(model) is IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item != null)
                            items.Add(Bind(item));
                    }
                }
                data.Collection(collection.Tag, items);
            }
        }

        return data;
    }
}

/// <summary>Tag như gõ trong Word; Within = khối lặp chứa tag (null = cấp tài liệu).</summary>
public sealed record TemplateTagInfo(string Tag, string Kind, string? Within, string? ConditionName = null);

public static class TemplateTagCatalog
{
    public const string FieldKind = "field";

    public const string RepeatKind = "repeat";

    public const string IfKind = "if";

    public const string IfNotKind = "ifnot";

    public static IReadOnlyList<TemplateTagInfo> Describe(Type formDataType)
    {
        ArgumentNullException.ThrowIfNull(formDataType);
        var result = new List<TemplateTagInfo>();
        Collect(formDataType, null, result, new HashSet<Type>());
        return result;
    }

    /// <summary>Dữ liệu giả để sinh thử: mọi trường có giá trị, điều kiện đúng, mỗi danh sách hai phần tử.</summary>
    public static TemplateData SampleData(Type formDataType)
    {
        ArgumentNullException.ThrowIfNull(formDataType);
        return Sample(formDataType, new HashSet<Type>());
    }

    private static void Collect(Type type, string? within, List<TemplateTagInfo> result, HashSet<Type> visiting)
    {
        if (!visiting.Add(type))
            return;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<TemplateFieldAttribute>() is { } field)
            {
                result.Add(new TemplateTagInfo(field.Tag, FieldKind, within));
            }
            else if (property.GetCustomAttribute<TemplateConditionAttribute>() is { } condition)
            {
                result.Add(new TemplateTagInfo("if:" + condition.Tag, IfKind, within, condition.Tag));
                result.Add(new TemplateTagInfo("ifnot:" + condition.Tag, IfNotKind, within, condition.Tag));
            }
            else if (property.GetCustomAttribute<TemplateCollectionAttribute>() is { } collection)
            {
                result.Add(new TemplateTagInfo("repeat:" + collection.Tag, RepeatKind, within));
                if (ElementType(property.PropertyType) is { } element)
                    Collect(element, collection.Tag, result, visiting);
            }
        }
        visiting.Remove(type);
    }

    private static TemplateData Sample(Type type, HashSet<Type> visiting)
    {
        var data = new TemplateData();
        if (!visiting.Add(type))
            return data;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<TemplateFieldAttribute>() is { } field)
            {
                data.Field(field.Tag, "Mẫu " + field.Tag);
            }
            else if (property.GetCustomAttribute<TemplateConditionAttribute>() is { } condition)
            {
                data.Condition(condition.Tag, true);
            }
            else if (property.GetCustomAttribute<TemplateCollectionAttribute>() is { } collection)
            {
                var element = ElementType(property.PropertyType);
                var items = new List<TemplateData>();
                if (element != null)
                {
                    items.Add(Sample(element, visiting));
                    items.Add(Sample(element, visiting));
                }
                data.Collection(collection.Tag, items);
            }
        }
        visiting.Remove(type);
        return data;
    }

    private static Type? ElementType(Type collectionType)
    {
        if (collectionType.IsArray)
            return collectionType.GetElementType();
        var enumerable = collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? collectionType
            : collectionType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0];
    }
}

/// <summary>Thông tin đơn vị dùng chung cho biểu mẫu Word; trường rỗng → null (giữ chữ mặc định).</summary>
public sealed class OrganizationTemplateFields
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    [TemplateField("ORG_PARTY_NAME")] public string? PartyCommitteeName { get; init; }

    [TemplateField("ORG_SUPERIOR_PARTY_NAME")] public string? SuperiorPartyName { get; init; }

    [TemplateField("ORG_COMPANY_NAME")] public string? CompanyName { get; init; }

    [TemplateField("ORG_COMPANY_NAME_UPPER")] public string? CompanyNameUpper { get; init; }

    [TemplateField("ORG_PARENT_COMPANY_NAME")] public string? ParentCompanyName { get; init; }

    [TemplateField("ORG_PARENT_COMPANY_NAME_UPPER")] public string? ParentCompanyNameUpper { get; init; }

    [TemplateField("ORG_SHORT_NAME")] public string? ShortName { get; init; }

    /// <summary>Địa danh ở dòng "…, ngày … tháng … năm …".</summary>
    [TemplateField("ORG_LOCATION")] public string? Location { get; init; }

    /// <summary>Tiêu đề trái Mẫu 10.</summary>
    [TemplateField("ORG_APPRAISAL_UNIT_UPPER")] public string? AppraisalUnitNameUpper { get; init; }

    public static IReadOnlyList<TemplateTagInfo> Tags { get; } = TemplateTagCatalog.Describe(typeof(OrganizationTemplateFields));

    public static OrganizationTemplateFields From(
        string? partyCommitteeName, string? superiorPartyName, string? companyName,
        string? parentCompanyName, string? shortName, string? location, string? appraisalUnitName = null) => new()
    {
        PartyCommitteeName = OrNull(partyCommitteeName),
        SuperiorPartyName = OrNull(superiorPartyName),
        CompanyName = OrNull(companyName),
        CompanyNameUpper = OrNull(companyName)?.ToUpper(Vietnamese),
        ParentCompanyName = OrNull(parentCompanyName),
        ParentCompanyNameUpper = OrNull(parentCompanyName)?.ToUpper(Vietnamese),
        ShortName = OrNull(shortName),
        Location = OrNull(location),
        AppraisalUnitNameUpper = OrNull(appraisalUnitName)?.ToUpper(Vietnamese)
    };

    private static string? OrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class TemplateDataExtensions
{
    /// <summary>Dữ liệu riêng của mẫu được ưu tiên hơn dữ liệu dùng chung.</summary>
    public static TemplateData WithShared(this TemplateData data, TemplateData shared)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(shared);
        foreach (var (key, value) in shared.Fields)
            data.Fields.TryAdd(key, value);
        foreach (var (key, value) in shared.Conditions)
            data.Conditions.TryAdd(key, value);
        foreach (var (key, value) in shared.Collections)
            data.Collections.TryAdd(key, value);
        return data;
    }
}
