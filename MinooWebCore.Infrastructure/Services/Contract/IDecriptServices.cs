using System;
using System.Collections.Generic;
using System.Text;

namespace MinooWebCore.Infrastructure.Services.Contract
{
    public interface IDecriptServices
    {
        Task<string> Decript(string encriptText);
        Task<string> Encript(string decriptText);

    }
}
