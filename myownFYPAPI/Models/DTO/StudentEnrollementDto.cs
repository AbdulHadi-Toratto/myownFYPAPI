using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class StudentEnrollementDto
    {
        public int EnrollmentID { get; set; }
        public string CourseCode { get; set; }
        public string CourseTitle { get; set; }
        public string TeacherName { get; set; }
        public string SessionName { get; set; }
    }
}