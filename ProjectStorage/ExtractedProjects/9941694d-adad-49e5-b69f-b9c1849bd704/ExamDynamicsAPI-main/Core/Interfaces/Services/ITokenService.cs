using ExamDynamicsAPI.Core.Models;
using System.Threading.Tasks;

namespace ExamDynamicsAPI.Core.Interfaces.Services
{
    public interface ITokenService
    {
        /// <summary>
        /// Generates a JWT token for the given user.
        /// </summary>
        /// <param name="user">The ApplicationUser instance.</param>
        /// <returns>A JWT token as a string.</returns>
        Task<string> GenerateJwtTokenAsync(ApplicationUser user);
    }
}
