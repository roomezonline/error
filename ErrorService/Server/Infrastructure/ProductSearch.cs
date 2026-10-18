using ErrorService.Server.Models;
using ErrorService.Shared;

namespace ErrorService.Server.Infrastructure;

public static class ProductSearch
{
    private const string Sentinel = "\u0001";

    public static IQueryable<Product> ApplyTextSearch(this IQueryable<Product> query, string? search)
    {
        var variants = SearchNormalizer.QueryVariants(search);
        while (variants.Count < 4)
            variants.Add(Sentinel);

        var a = variants[0];
        var b = variants[1];
        var c = variants[2];
        var d = variants[3];

        return query.Where(p =>
            p.Name.Contains(a) || p.Name.Contains(b) || p.Name.Contains(c) || p.Name.Contains(d) ||
            (p.Description != null &&
                (p.Description.Contains(a) || p.Description.Contains(b) || p.Description.Contains(c) || p.Description.Contains(d))) ||
            (p.Sku != null &&
                (p.Sku.Contains(a) || p.Sku.Contains(b) || p.Sku.Contains(c) || p.Sku.Contains(d))) ||
            (p.Slug != null &&
                (p.Slug.Contains(a) || p.Slug.Contains(b) || p.Slug.Contains(c) || p.Slug.Contains(d))) ||
            (p.Category != null &&
                (p.Category.Name.Contains(a) || p.Category.Name.Contains(b) || p.Category.Name.Contains(c) || p.Category.Name.Contains(d))));
    }
}
