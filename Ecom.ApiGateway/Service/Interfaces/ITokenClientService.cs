namespace Ecom.ApiGateway.Service.Interfaces
{
    public interface ITokenClientService
    {
        public Task<string> GetSystemTokenAsync();
    }
}
