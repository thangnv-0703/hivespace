namespace Catalog.Application.Features.Categories;

/// <summary>
/// The advisory lock key for the category tree. A load takes it exclusively; FEAT-012's product create must take
/// <c>pg_advisory_xact_lock_shared</c> with the same key before it checks the category (design D-7, §8).
/// </summary>
public static class CategoryTreeLock
{
    public const long Key = 0x4856_4341_5452_0001;
}
