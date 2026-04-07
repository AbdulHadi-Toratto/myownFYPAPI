using myownFYPAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace myownFYPAPI.Controllers.Director
{
    [RoutePrefix("api/director/performance")]
    public class PerformanceController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();

        [HttpGet]
        [Route("GetSessions")]
        public IHttpActionResult GetSessions()
        {
            var sessions = db.Session
                .Select(s => new
                {
                    id = s.id,
                    name = s.name
                }).ToList();

            return Ok(sessions);
        }

        [HttpGet]
        [Route("GetCoursesBySession")]
        public IHttpActionResult GetCoursesBySession(int sessionId)
        {
            var courses = db.Enrollment
                .Where(e => e.sessionID == sessionId)
                .Select(e => e.courseCode)
                .Distinct()
                .ToList();

            return Ok(courses);
        }


        [HttpGet]
        [Route("GetTeachersByCourse")]
        public IHttpActionResult GetTeachersByCourse(string courseCode)
        {
            if (string.IsNullOrEmpty(courseCode))
                return Ok(new List<object>());

            courseCode = courseCode.Trim().ToUpper(); // normalize

            // Get latest session ID for this course
            var latestSessionId = db.Enrollment
                .Where(e => e.courseCode.ToUpper().Trim() == courseCode)
                .OrderByDescending(e => e.sessionID)
                .Select(e => e.sessionID)
                .FirstOrDefault();

            var teachers = db.Enrollment
                .Where(e => e.courseCode.ToUpper().Trim() == courseCode && e.sessionID == latestSessionId)
                .Select(e => new
                {
                    id = e.teacherID,
                    name = db.Teacher
                                .Where(t => t.userID == e.teacherID)
                                .Select(t => t.name)
                                .FirstOrDefault()
                })
                .Distinct()
                .ToList();

            return Ok(teachers);
        }

        [HttpGet]
        [Route("GetTeacherPerformance")]
        public IHttpActionResult GetTeacherPerformance(int sessionId, string department = null, string courseCode = null)
        {
            var query = db.Enrollment.Where(e => e.sessionID == sessionId);

            if (!string.IsNullOrEmpty(courseCode) && courseCode != "All")
            {
                query = query.Where(e => e.courseCode == courseCode);
            }

            var data = query
                .GroupBy(e => new { e.teacherID, e.courseCode })
                .Select(g => new
                {
                    TeacherID = g.Key.teacherID,
                    CourseCode = g.Key.courseCode,

                    TeacherName = db.Teacher
                        .Where(t => t.userID == g.Key.teacherID)
                        .Select(t => t.name)
                        .FirstOrDefault(),

                    Department = db.Teacher
                        .Where(t => t.userID == g.Key.teacherID)
                        .Select(t => t.department)
                        .FirstOrDefault(),

                    // ✅ Peer Evaluation Avg
                    PeerAvg = db.PeerEvaluation
                        .Where(p =>
                            p.evaluateeID == g.Key.teacherID &&
                            p.courseCode == g.Key.courseCode &&
                            p.SessionId == sessionId
                        )
                        .Average(p => (int?)p.score),

                    // ✅ Student Evaluation Avg (FIXED JOIN)
                    StudentAvg = db.StudentEvaluation
                        .Where(s =>
                            s.SessionID == sessionId &&
                            s.Enrollment.teacherID == g.Key.teacherID &&
                            s.Enrollment.courseCode == g.Key.courseCode
                        )
                        .Average(s => (int?)s.score)
                })
                .ToList();

            // ✅ APPLY DEPARTMENT FILTER
            if (!string.IsNullOrEmpty(department))
            {
                data = data.Where(d => d.Department == department).ToList();
            }

            // ✅ FINAL RESULT WITH COMBINED AVERAGE
            var result = data.Select(x =>
            {
                var peer = x.PeerAvg ?? 0;
                var student = x.StudentAvg ?? 0;

                int count = 0;
                if (x.PeerAvg != null) count++;
                if (x.StudentAvg != null) count++;

                var finalAvg = count > 0 ? (peer + student) / count : 0;

                return new
                {
                    x.TeacherID,
                    x.TeacherName,
                    x.CourseCode,
                    x.Department,
                    Percentage = (finalAvg / 4.0) * 100
                };
            });

            return Ok(result);
        }
    }
}
