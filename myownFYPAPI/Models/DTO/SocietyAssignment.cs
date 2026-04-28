using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class SocietyAssignment
    {
        public int AssignmentId { get; set; }
        public string TeacherId { get; set; }
        public int SocietyId { get; set; }
        public int SessionId { get; set; }
        public bool IsChairperson { get; set; }
        public bool IsMentor
        {
            get; set;
        }
    }
}