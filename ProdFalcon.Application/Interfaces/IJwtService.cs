using ProdFalcon.Domain.Entities;

namespace ProdFalcon.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(AppUser user);
}