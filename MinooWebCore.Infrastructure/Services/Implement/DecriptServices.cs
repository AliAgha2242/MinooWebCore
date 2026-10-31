using Decode;
using MinooWebCore.Infrastructure.Services.Contract;
using System;
using System.Collections.Generic;
using System.Security.Permissions;
using System.Text;

namespace MinooWebCore.Infrastructure.Services.implementaion
{
    public class DecriptServices : IDecriptServices
    {


        public DecriptServices()
        {

        }
        public Task<string> Decript(string encriptText)
        {
            return Task.Run(() =>
            {
                return string.Concat(DecodePass.Security.Decode(encriptText));
            });
        }

        public Task<string> Encript(string decriptText)
        {
            return Task.Run(() =>
            {
                return string.Concat(DecodePass.Security.Encode(decriptText));
            });
        }
    }
}
