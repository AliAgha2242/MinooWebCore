using Decode;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MinooWebCore.Data;
using MinooWebCore.Infrastructure.Dapper;
using MinooWebCore.Infrastructure.Services.Contract;
using MinooWebCore.Infrastructure.Services.implementaion;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Text;

namespace MinooWebCore.Infrastructure
{
    public static class InfraDiRegister
    {
        public static IServiceCollection infrasreg(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not configured.");

            services.AddSingleton<IDecriptServices, DecriptServices>();

            services.AddDbContext<MinooDbContext>(options =>
                options.UseSqlServer(connectionString, sqlOptions => sqlOptions.CommandTimeout(0)));

            services.AddScoped<IDapperRepository, DapperRepository>();

            return services;
        }
    }
}
