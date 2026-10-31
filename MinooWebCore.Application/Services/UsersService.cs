using MinooWebCore.Data;
using MinooWebCore.Data.Models;
using MinooWebCore.Infrastructure.Services.Contract;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MinooWebCore.Application.Services
{
    public class UsersService : IUsersService
    {
        public IGenericRepository<UserModel> repository { get; set; }
        public IDecriptServices decriptService { get; set; }

        public UsersService(IDecriptServices decriptService, IGenericRepository<UserModel> repository)
        {
            this.decriptService = decriptService;
            this.repository = repository;
        }

        public async Task<UserModel?> GetAsync(int Userid)
        {
            UserModel? user = await repository.GetByIdAsync(Userid);
            if (user != null)
            {
                user.UserPassword = await decriptService.Decript(user.UserPassword);
            }
            return user;
        }

        public async Task<string> ResetPass(UserModel user)
        {
            Random rand = new Random(100000);
            string newPass = rand.Next(10000000).ToString();
            user.UserPassword = await decriptService.Decript(newPass);
            repository.Update(user);
            await repository.SaveChangesAsync();
            return newPass;
        }
    }
}
