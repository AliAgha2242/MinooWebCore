using MinooWebCore.Data;
using MinooWebCore.Data.Models;
using MinooWebCore.Infrastructure.Services.Contract;
using System;
using System.Collections.Generic;
using System.Text;

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
                user.Password = await decriptService.Decript(user.Password);
            }
            return user;
        }
    }
}
