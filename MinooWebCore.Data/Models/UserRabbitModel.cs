using System;
using System.Collections.Generic;
using System.Text;

namespace MinooWebCore.Data.Models
{
    public class UserRabbitModel
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public UserRabbitModel(string userName, string password)
        {
            UserName = userName;
            Password = password;
        }
    }
}
