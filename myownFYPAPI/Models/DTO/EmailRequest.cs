using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class EmailRequest
    {
        public string mail { get; set; }
        public string filter { get; set; } // "unread", "read", "all"
    }
}