namespace MiddleCsharp.DataAccess;

internal interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    void ClearChanges();
}
