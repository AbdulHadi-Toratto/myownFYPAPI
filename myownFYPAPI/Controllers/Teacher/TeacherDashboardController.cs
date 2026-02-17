using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using myownFYPAPI.Models;

namespace myownFYPAPI.Controllers.Teacher
{
        [RoutePrefix("api/TeacherDashboard")]
    public class TeacherDashboardController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();


        [HttpGet]
        [Route("GetTeacherName/{teacherId}")]
        public IHttpActionResult GetTeacherName(string teacherId)
        {
            if (string.IsNullOrEmpty(teacherId))
                return BadRequest("Teacher ID is required.");

            var teacher = db.Teacher
                            .Where(t => t.userID == teacherId)
                            .Select(t => t.name)
                            .FirstOrDefault();

            if (teacher == null)
                return NotFound();

            return Ok(teacher);
        }

        [HttpGet]
        [Route("GetPeerWithCourses/{teacherId}")]
        public IHttpActionResult GetPeerCourses(String teacherId)
        {
            var data = db.Enrollment
                .Where(e => e.teacherID != teacherId)
                .Select(e => new
                {
                    TeacherID = e.Teacher.userID,
                    TeacherName = e.Teacher.name,
                    CourseCode = e.courseCode
                })
                .Distinct()
                .ToList();

            return Ok(data);
        }

    }
}
