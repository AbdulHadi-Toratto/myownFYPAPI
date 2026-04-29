using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class CourseManagementDto
    {
        public class TeacherCourseResponse
        {
            public string TeacherID { get; set; }
            public string TeacherName { get; set; }
            public List<EnrolledCourseDTO> EnrolledCourses { get; set; }
        }

        public class EnrolledCourseDTO
        {
            public string Id { get; set; }
            public string Course { get; set; }
            public string Code { get; set; }
        }

        public class EvaluationDto
        {
            public string TeacherID { get; set; }
            public int SessionID { get; set; }
            public string HODID { get; set; }
            public string PaperStatus { get; set; }  // "OnTime" or "Late"
            public string FolderStatus { get; set; } // "OnTime" or "Late"
        }

        // In case you want to send evaluation for multiple courses at once in future
        public class EvaluationRequestDTO
        {
            public string TeacherID { get; set; }
            public int SessionID { get; set; }
            public string HODID { get; set; }
            public List<CourseEvalDTO> Evaluations { get; set; }
        }

        public class CourseEvalDTO
        {
            public string CourseCode { get; set; }
            public string PaperStatus { get; set; }
            public string FolderStatus { get; set; }
        }
    }
}