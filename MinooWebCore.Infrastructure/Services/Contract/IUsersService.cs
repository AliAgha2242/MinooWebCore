using MinooWebCore.Data;
using MinooWebCore.Data.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MinooWebCore.Infrastructure.Services.Contract
{
    public interface IUsersService
    {
        Task<UserModel?> GetAsync(int Userid);
        Task<string> ResetPass(UserModel user);
    }
}
