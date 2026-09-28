using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
