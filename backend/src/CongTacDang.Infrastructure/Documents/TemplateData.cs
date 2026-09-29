using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace CongTacDang.Infrastructure.Documents;

/// <summary>
/// Dữ liệu điền vào template Word: trường đơn, danh sách lặp (bảng lặp dòng) và điều kiện ẩn/hiện.
/// Khóa là Tag của Content Control trong template (không phân biệt hoa thường).
/// </summary>
public sealed class TemplateData
{
    /// <summary>Trường đơn. Giá trị null = giữ nguyên chữ mặc định trong template.</summary>
    public Dictionary<string, string?> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Điều kiện cho khối <c>if:</c>/<c>ifnot:</c>.</summary>
    public Dictionary<string, bool> Conditions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Danh sách cho khối <c>repeat:</c>; mỗi phần tử là dữ liệu của một lần lặp.</summary>
    public Dictionary<string, List<TemplateData>> Collections { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gán trường đơn.</summary>
    public TemplateData Field(string tag, string? value)
    {
        Fields[tag] = value;
        return this;
    }

    /// <summary>Gán điều kiện.</summary>
    public TemplateData Condition(string tag, bool value)
    {
        Conditions[tag] = value;
        return this;
    }

    /// <summary>Gán danh sách lặp.</summary>
    public TemplateData Collection(string tag, IEnumerable<TemplateData> items)
    {
        Collections[tag] = new List<TemplateData>(items);
        return this;
    }
}

/// <summary>Đánh dấu thuộc tính là trường đơn ứng với Content Control có Tag = <see cref="Tag"/>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class TemplateFieldAttribute : Attribute
{
    public TemplateFieldAttribute(string tag) => Tag = tag;

    /// <summary>Tag của Content Control.</summary>
    public string Tag { get; }
}

/// <summary>Đánh dấu thuộc tính danh sách ứng với khối lặp <c>repeat:Tag</c>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class TemplateCollectionAttribute : Attribute
{
    public TemplateCollectionAttribute(string tag) => Tag = tag;

    /// <summary>Tên danh sách (phần sau <c>repeat:</c>).</summary>
    public string Tag { get; }
}

/// <summary>Đánh dấu thuộc tính bool ứng với khối điều kiện <c>if:Tag</c> / <c>ifnot:Tag</c>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class TemplateConditionAttribute : Attribute
{
    public TemplateConditionAttribute(string tag) => Tag = tag;

    /// <summary>Tên điều kiện (phần sau <c>if:</c>/<c>ifnot:</c>).</summary>
    public string Tag { get; }
}

/// <summary>Chuyển lớp dữ liệu mẫu (có gắn thuộc tính Template*) thành <see cref="TemplateData"/>.</summary>
public static class TemplateDataBinder
{
    /// <summary>Đọc các thuộc tính có gắn <see cref="TemplateFieldAttribute"/>, <see cref="TemplateCollectionAttribute"/>, <see cref="TemplateConditionAttribute"/>.</summary>
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

/// <summary>Một tag trong danh mục tag của lớp dữ liệu mẫu.</summary>
/// <param name="Tag">Tag đúng như gõ trong Word (<c>FULL_NAME</c>, <c>repeat:TASKS</c>, <c>if:X</c>, <c>ifnot:X</c>).</param>
/// <param name="Kind">Loại: <c>field</c>, <c>repeat</c>, <c>if</c>, <c>ifnot</c>.</param>
/// <param name="Within">Tên khối lặp chứa tag; null = cấp tài liệu.</param>
/// <param name="ConditionName">Tên điều kiện (với <c>if</c>/<c>ifnot</c>).</param>
public sealed record TemplateTagInfo(string Tag, string Kind, string? Within, string? ConditionName = null);

/// <summary>Dựng danh mục tag từ lớp dữ liệu mẫu (thuộc tính Template*), gồm cả tag của dòng lặp.</summary>
public static class TemplateTagCatalog
{
    /// <summary>Loại tag trường đơn.</summary>
    public const string FieldKind = "field";

    /// <summary>Loại tag khối lặp.</summary>
    public const string RepeatKind = "repeat";

    /// <summary>Loại tag khối điều kiện đúng.</summary>
    public const string IfKind = "if";

    /// <summary>Loại tag khối điều kiện sai.</summary>
    public const string IfNotKind = "ifnot";

    /// <summary>Danh mục tag của lớp dữ liệu mẫu <paramref name="formDataType"/>.</summary>
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

/// <summary>
/// Thông tin đơn vị dùng chung cho mọi biểu mẫu Word (task 17 — T-80). Giá trị lấy từ cài đặt đơn vị; trường rỗng → null
/// (giữ chữ mặc định trong template). Tag là tùy chọn: template nào cần thì đặt Content Control tương ứng.
/// </summary>
public sealed class OrganizationTemplateFields
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Tên Đảng bộ.</summary>
    [TemplateField("ORG_PARTY_NAME")] public string? PartyCommitteeName { get; init; }

    /// <summary>Tên tổ chức Đảng cấp trên.</summary>
    [TemplateField("ORG_SUPERIOR_PARTY_NAME")] public string? SuperiorPartyName { get; init; }

    /// <summary>Tên đầy đủ của công ty.</summary>
    [TemplateField("ORG_COMPANY_NAME")] public string? CompanyName { get; init; }

    /// <summary>Tên đầy đủ của công ty, chữ in hoa.</summary>
    [TemplateField("ORG_COMPANY_NAME_UPPER")] public string? CompanyNameUpper { get; init; }

    /// <summary>Tên đơn vị chủ quản cấp trên.</summary>
    [TemplateField("ORG_PARENT_COMPANY_NAME")] public string? ParentCompanyName { get; init; }

    /// <summary>Tên đơn vị chủ quản cấp trên, chữ in hoa.</summary>
    [TemplateField("ORG_PARENT_COMPANY_NAME_UPPER")] public string? ParentCompanyNameUpper { get; init; }

    /// <summary>Tên viết tắt.</summary>
    [TemplateField("ORG_SHORT_NAME")] public string? ShortName { get; init; }

    /// <summary>Địa danh ở dòng "…, ngày … tháng … năm …".</summary>
    [TemplateField("ORG_LOCATION")] public string? Location { get; init; }

    /// <summary>Danh mục tag thông tin đơn vị (dùng chung, tùy chọn).</summary>
    public static IReadOnlyList<TemplateTagInfo> Tags { get; } = TemplateTagCatalog.Describe(typeof(OrganizationTemplateFields));

    /// <summary>Dựng từ các giá trị cài đặt (chuỗi rỗng → null: giữ chữ mặc định của template).</summary>
    public static OrganizationTemplateFields From(
        string? partyCommitteeName, string? superiorPartyName, string? companyName,
        string? parentCompanyName, string? shortName, string? location) => new()
    {
        PartyCommitteeName = OrNull(partyCommitteeName),
        SuperiorPartyName = OrNull(superiorPartyName),
        CompanyName = OrNull(companyName),
        CompanyNameUpper = OrNull(companyName)?.ToUpper(Vietnamese),
        ParentCompanyName = OrNull(parentCompanyName),
        ParentCompanyNameUpper = OrNull(parentCompanyName)?.ToUpper(Vietnamese),
        ShortName = OrNull(shortName),
        Location = OrNull(location)
    };

    private static string? OrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Ghép dữ liệu dùng chung (thông tin đơn vị) vào dữ liệu của biểu mẫu.</summary>
public static class TemplateDataExtensions
{
    /// <summary>
    /// Thêm trường/điều kiện/danh sách của <paramref name="shared"/> mà <paramref name="data"/> chưa có (dữ liệu riêng của mẫu được ưu tiên).
    /// </summary>
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
