using Ecom.ApiGateway.Models.Auths;

namespace Ecom.ApiGateway.Service.Interfaces
{
    public interface IUserCacheService
    {
        Task<UserInternalInfo?> GetUserInfoAsync(string userId);
    }
}
