using System;
using System.Collections.Generic;
using System.Linq;

namespace CongTacDang.Application.Organization;

/// <summary>Một nút cây đơn vị dùng để tính đường dẫn (Id + Id cha).</summary>
/// <param name="Id">Đơn vị.</param>
/// <param name="ParentId">Đơn vị cha (null = gốc).</param>
public readonly record struct OrgNodeLink(Guid Id, Guid? ParentId);

/// <summary>Kết quả dựng đường dẫn cây.</summary>
/// <param name="Paths">Đường dẫn vật hóa của mọi nút hợp lệ.</param>
/// <param name="CycleNodes">Các nút nằm trên (hoặc treo dưới) một vòng lặp cha–con.</param>
/// <param name="MissingParentNodes">Các nút có cha không tồn tại trong tập nút.</param>
public sealed record OrgTreeBuildResult(
    IReadOnlyDictionary<Guid, string> Paths,
    IReadOnlyCollection<Guid> CycleNodes,
    IReadOnlyCollection<Guid> MissingParentNodes)
{
    /// <summary>Không có vòng và không thiếu cha.</summary>
    public bool IsValid => CycleNodes.Count == 0 && MissingParentNodes.Count == 0;
}

/// <summary>
/// Quy tắc cây đơn vị (tổ chức Đảng / đơn vị chính quyền) theo đường dẫn vật hóa <c>/&lt;id gốc&gt;/…/&lt;id nút&gt;/</c>.
/// Mọi thay đổi cha được tính lại trên toàn bộ tập nút (cây nhỏ — vài chục đến vài trăm đơn vị) để không cần
/// cập nhật tiền tố từng phần và phát hiện vòng lặp chắc chắn.
/// </summary>
public static class OrgTree
{
    /// <summary>Đoạn đường dẫn của một nút (<c>&lt;guid&gt;/</c>).</summary>
    public static string Segment(Guid id) => id.ToString("D") + "/";

    /// <summary>Đường dẫn của nút <paramref name="id"/> dưới cha có đường dẫn <paramref name="parentPath"/> (null/rỗng = gốc).</summary>
    public static string BuildPath(string? parentPath, Guid id)
        => (string.IsNullOrEmpty(parentPath) ? "/" : parentPath) + Segment(id);

    /// <summary>Nút có đường dẫn <paramref name="path"/> là chính <paramref name="ancestorId"/> hoặc con cháu của nó.</summary>
    public static bool IsSelfOrDescendant(string? path, Guid ancestorId)
        => !string.IsNullOrEmpty(path) && path.Contains("/" + Segment(ancestorId), StringComparison.Ordinal);

    /// <summary>
    /// Dựng đường dẫn cho mọi nút từ quan hệ cha–con. Nút có cha không thuộc tập → <c>MissingParentNodes</c>;
    /// nút nằm trên vòng lặp hoặc treo dưới vòng lặp → <c>CycleNodes</c>. Hai loại nút lỗi không có đường dẫn.
    /// </summary>
    public static OrgTreeBuildResult BuildPaths(IEnumerable<OrgNodeLink> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var parents = new Dictionary<Guid, Guid?>();
        foreach (var node in nodes)
            parents[node.Id] = node.ParentId;

        var paths = new Dictionary<Guid, string>();
        var cycle = new HashSet<Guid>();
        var missing = new HashSet<Guid>();

        foreach (var start in parents.Keys)
        {
            if (paths.ContainsKey(start) || cycle.Contains(start) || missing.Contains(start))
                continue;

            // Đi ngược lên gốc, ghi lại chuỗi nút chưa có đường dẫn.
            var chain = new List<Guid>();
            var onChain = new HashSet<Guid>();
            var current = start;
            string? basePath = null;
            var failure = 0; // 0 = tới gốc/nút đã biết, 1 = vòng, 2 = thiếu cha
            while (true)
            {
                if (paths.TryGetValue(current, out var known))
                {
                    basePath = known;
                    break;
                }
                if (cycle.Contains(current))
                {
                    failure = 1;
                    break;
                }
                if (missing.Contains(current))
                {
                    failure = 2;
                    break;
                }
                if (!onChain.Add(current))
                {
                    failure = 1;
                    break;
                }
                chain.Add(current);

                var parent = parents[current];
                if (parent == null)
                    break;
                if (!parents.ContainsKey(parent.Value))
                {
                    // Nút cuối chuỗi thiếu cha; mọi nút dưới nó cũng không có đường dẫn.
                    failure = 2;
                    break;
                }
                current = parent.Value;
            }

            if (failure == 1)
            {
                cycle.UnionWith(chain);
                continue;
            }
            if (failure == 2)
            {
                missing.UnionWith(chain);
                continue;
            }

            // Gán đường dẫn từ trên xuống.
            for (var i = chain.Count - 1; i >= 0; i--)
            {
                basePath = BuildPath(basePath, chain[i]);
                paths[chain[i]] = basePath;
            }
        }

        return new OrgTreeBuildResult(paths, cycle, missing);
    }

    /// <summary>Id các nút là <paramref name="rootId"/> hoặc con cháu của nó (theo đường dẫn).</summary>
    public static IReadOnlyList<Guid> SelfAndDescendants(IEnumerable<(Guid Id, string Path)> nodes, Guid rootId)
        => nodes.Where(n => n.Id == rootId || IsSelfOrDescendant(n.Path, rootId)).Select(n => n.Id).Distinct().ToList();
}
