using Domain;
using Microsoft.EntityFrameworkCore;

namespace Application;

/// <summary>
/// This interface will be used in our queries.
/// Wr'te not using repositories because this will be used as a read only context and so we don't need to protect any invariants.
/// </summary>
public interface IQueryContext
{
    DbSet<Product> Products {get;}
    DbSet<Order> Orders {get;}
    DbSet<Voucher> Vouchers { get; }
}
