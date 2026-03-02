namespace SMS.Core.Interfaces;

public interface IEntityCrudService<TDto, in TCreateRequest>
{
    Task<IReadOnlyList<TDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TDto> CreateAsync(TCreateRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}


