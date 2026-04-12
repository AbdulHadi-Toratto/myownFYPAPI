using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class CompareDTO
    {
        public string mode { get; set; }
        public string teacherA { get; set; }
        public string teacherB { get; set; }
        public string courseCode { get; set; }
        public int? session1 { get; set; }
        public int? session2 { get; set; }
    }
}