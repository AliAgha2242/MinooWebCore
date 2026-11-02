namespace MinooWebCore.Data.Models;

public class UserModel
{
    public decimal UsersKeyId { get; set; }
    public int UserId { get; set; }
    public string UserCode { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public decimal? VahedeTejariSn { get; set; }
    public string UserPassword { get; set; } = string.Empty;
    public string ExpireDate { get; set; } = string.Empty;
}