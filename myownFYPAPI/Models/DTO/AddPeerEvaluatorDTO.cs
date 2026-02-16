using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class AddPeerEvaluatorDTO
    {
        public int SessionId { get; set; }
        public List<int> TeacherIds { get; set; }
    }
}