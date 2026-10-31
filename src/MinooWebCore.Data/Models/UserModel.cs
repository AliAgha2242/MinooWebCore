using System;
using System.Collections.Generic;
using System.Text;

namespace MinooWebCore.Data.Models
{
    public class UserModel 
    {
        public decimal UserKeyId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public decimal vahedeTejariSn { get; set; }
        public string Password { get; set; }
    }
}
