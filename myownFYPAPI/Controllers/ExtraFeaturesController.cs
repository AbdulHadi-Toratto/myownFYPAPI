using myownFYPAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace myownFYPAPI.Controllers
{
    [RoutePrefix("api/ExtraFeatures")]
    public class ExtraFeaturesController : ApiController
    {
        
            fypapiv1Entities db = new fypapiv1Entities();

            // GET api/TeacherSelf/GetSessions
            // ─────────────────────────────────────────────────────────────────────
            [HttpGet]
            [Route("GetSessions")]
            public IHttpActionResult GetSessions()
            {
                return Ok(db.Session
                    .OrderByDescending(s => s.id)
                    .Select(s => new { s.id, s.name })
                    .ToList());
            }



            //teacher aginst seesion
            [HttpGet]
            [Route("GetTeachersBySession/{sessionId}")]
            public IHttpActionResult GetTeachersBySession(int sessionId)
            {
                try
                {
                    var enrolledTeachers = db.Enrollment
                        .Where(e => e.sessionID == sessionId)
                        .Select(e => new {
                            UserID = e.Teacher.userID,
                            Name = e.Teacher.name
                        })
                        .Distinct()
                        .ToList();

                    return Ok(enrolledTeachers);
                }
                catch (Exception ex) { return InternalServerError(ex); }
            }

            // ─────────────────────────────────────────────────────────────────────
            // GET api/TeacherSelf/GetMyCourses/{teacherId}/{sessionId}
            // Returns distinct course codes for a teacher in a session
            // ─────────────────────────────────────────────────────────────────────
            [HttpGet]
            [Route("GetMyCourses/{teacherId}/{sessionId}")]
            public IHttpActionResult GetMyCourses(string teacherId, int sessionId)
            {
                var courses = db.Enrollment
                    .Where(e => e.teacherID == teacherId && e.sessionID == sessionId)
                    .Select(e => e.courseCode)
                    .Distinct()
                    .ToList();
                return Ok(courses);
            }

            // ─────────────────────────────────────────────────────────────────────
            // GET api/TeacherSelf/GetMyPerformance/{teacherId}/{sessionId}
            // Overall performance summary (Student + Peer + CHR)
            // ─────────────────────────────────────────────────────────────────────
            [HttpGet]
            [Route("GetMyPerformance/{teacherId}/{sessionId}")]
            public IHttpActionResult GetMyPerformance(string teacherId, int sessionId)
            {
                const double MAX = 4.0;
                const double SCALE = 10.0;

                var studentList = db.StudentEvaluation
                    .Where(s => s.Enrollment.teacherID == teacherId
                             && s.Enrollment.sessionID == sessionId)
                    .ToList();
                double sAvg = studentList.Any()
                    ? (studentList.Sum(s => (double)s.score) / (studentList.Count * MAX)) * SCALE : 0;

                var peerList = db.PeerEvaluation
                    .Where(p => p.evaluateeID == teacherId
                             && p.PeerEvaluator.sessionID == sessionId)
                    .ToList();
                double pAvg = peerList.Any()
                    ? (peerList.Sum(p => (double)p.score) / (peerList.Count * MAX)) * SCALE : 0;

                var isEnrolled = db.Enrollment
                    .Any(e => e.teacherID == teacherId && e.sessionID == sessionId);

                double chrAvg = 0.0;
                if (isEnrolled)
                {
                    var chrRawData = db.CHR
                        .Where(c => c.TeacherID == teacherId)
                        .Select(x => new { LateIn = x.LateIn ?? 0, LeftEarly = x.LeftEarly ?? 0 })
                        .ToList();
                    chrAvg = chrRawData.Any()
                        ? chrRawData.Select(x => {
                            int total = x.LateIn + x.LeftEarly;
                            if (total >= 10) return 0.0;
                            if (total >= 6) return 3.0;
                            if (total >= 1) return 4.0;
                            return 5.0;
                        }).Average() : 0.0;
                }
                double chrPerc = Math.Round((chrAvg / 5.0) * SCALE, 2);

                var courses = db.Enrollment
                    .Where(e => e.teacherID == teacherId && e.sessionID == sessionId)
                    .Select(e => e.courseCode)
                    .Distinct()
                    .ToList();

                var teacher = db.Teacher.FirstOrDefault(t => t.userID == teacherId);

                return Ok(new
                {
                    TeacherID = teacherId,
                    Name = teacher?.name,
                    Department = teacher?.department,
                    StudentAverage = Math.Round(sAvg, 2),
                    PeerAverage = Math.Round(pAvg, 2),
                    ChrAverage = chrPerc,
                    ChrRawScore = Math.Round(chrAvg, 2),
                    Courses = courses,
                    OverallPercentage = Math.Round(((sAvg + pAvg + chrPerc) / 30.0) * 100, 2)
                });
            }

            // ─────────────────────────────────────────────────────────────────────
            // GET api/TeacherSelf/GetCourseComparison/{teacherId}/{sessionId}
            //     ?evaluationType=both|student|peer
            //
            // ✅ NEW: evaluationType parameter added
            //    - student → only StudentAverage filled, PeerAverage = 0
            //    - peer    → only PeerAverage filled, StudentAverage = 0
            //    - both    → both filled (default behaviour)
            // ─────────────────────────────────────────────────────────────────────
            [HttpGet]
            [Route("GetCourseComparison/{teacherId}/{sessionId}")]
            public IHttpActionResult GetCourseComparison(
                string teacherId,
                int sessionId,
                [FromUri] string evaluationType = "both")
            {
                const double MAX = 4.0;
                const double SCALE = 10.0;

                // Null safety — default to "both" if missing
                if (string.IsNullOrWhiteSpace(evaluationType))
                    evaluationType = "both";
                evaluationType = evaluationType.ToLower().Trim();

                var courses = db.Enrollment
                    .Where(e => e.teacherID == teacherId && e.sessionID == sessionId)
                    .Select(e => e.courseCode)
                    .Distinct()
                    .ToList();

                var result = courses.Select(course =>
                {
                    double sAvg = 0;
                    double pAvg = 0;

                    // ── Student average (skip if peer-only) ──────────────────────
                    if (evaluationType == "student" || evaluationType == "both")
                    {
                        var studentList = db.StudentEvaluation
                            .Where(s => s.Enrollment.teacherID == teacherId
                                     && s.Enrollment.sessionID == sessionId
                                     && s.Enrollment.courseCode == course)
                            .ToList();
                        sAvg = studentList.Any()
                            ? (studentList.Sum(s => (double)s.score) / (studentList.Count * MAX)) * SCALE
                            : 0;
                    }

                    // ── Peer average (skip if student-only) ──────────────────────
                    if (evaluationType == "peer" || evaluationType == "both")
                    {
                        var peerList = db.PeerEvaluation
                            .Where(p => p.evaluateeID == teacherId
                                     && p.PeerEvaluator.sessionID == sessionId
                                     && p.courseCode == course)
                            .ToList();
                        pAvg = peerList.Any()
                            ? (peerList.Sum(p => (double)p.score) / (peerList.Count * MAX)) * SCALE
                            : 0;
                    }

                    // ── Overall % based on what is included ─────────────────────
                    double overall = 0;
                    if (evaluationType == "both")
                        overall = Math.Round(((sAvg + pAvg) / 20.0) * 100, 2);
                    else if (evaluationType == "student")
                        overall = Math.Round((sAvg / 10.0) * 100, 2);
                    else if (evaluationType == "peer")
                        overall = Math.Round((pAvg / 10.0) * 100, 2);

                    return new
                    {
                        CourseCode = course,
                        StudentAverage = Math.Round(sAvg, 2),
                        PeerAverage = Math.Round(pAvg, 2),
                        Overall = overall
                    };
                }).ToList();

                return Ok(result);
            }

            // ─────────────────────────────────────────────────────────────────────
            // GET api/TeacherSelf/GetMyQuestionStats/{teacherId}/{sessionId}
            //     ?courseCode=CS101&evaluationType=both|student|peer
            // ─────────────────────────────────────────────────────────────────────
            [HttpGet]
            [Route("GetMyQuestionStats/{teacherId}/{sessionId}")]
            public IHttpActionResult GetMyQuestionStats(
                string teacherId,
                int sessionId,
                string courseCode = null,
                string evaluationType = "both")
            {
                var result = new List<object>();

                if (evaluationType == "student" || evaluationType == "both")
                {
                    var studentData = db.StudentEvaluation
                        .Where(s => s.Enrollment.teacherID == teacherId
                                 && s.Enrollment.sessionID == sessionId
                                 && (string.IsNullOrEmpty(courseCode) || s.Enrollment.courseCode == courseCode))
                        .GroupBy(s => s.questionID)
                        .Select(g => new
                        {
                            QuestionId = g.Key,
                            QuestionText = db.Questions.Where(q => q.QuestionID == g.Key).Select(q => q.QuestionText).FirstOrDefault(),
                            AverageScore = g.Average(x => (double?)x.score) ?? 0,
                            TotalResponses = g.Count(),
                            Score1 = g.Count(x => x.score == 1),
                            Score2 = g.Count(x => x.score == 2),
                            Score3 = g.Count(x => x.score == 3),
                            Score4 = g.Count(x => x.score == 4),
                            Type = "Student"
                        }).ToList();
                    result.AddRange(studentData);
                }

                if (evaluationType == "peer" || evaluationType == "both")
                {
                    var peerData = db.PeerEvaluation
                        .Where(p => p.evaluateeID == teacherId
                                 && p.PeerEvaluator.sessionID == sessionId
                                 && (string.IsNullOrEmpty(courseCode) || p.courseCode == courseCode))
                        .GroupBy(p => p.questionID)
                        .Select(g => new
                        {
                            QuestionId = g.Key,
                            QuestionText = db.Questions.Where(q => q.QuestionID == g.Key).Select(q => q.QuestionText).FirstOrDefault(),
                            AverageScore = g.Average(x => (double?)x.score) ?? 0,
                            TotalResponses = g.Count(),
                            Score1 = g.Count(x => x.score == 1),
                            Score2 = g.Count(x => x.score == 2),
                            Score3 = g.Count(x => x.score == 3),
                            Score4 = g.Count(x => x.score == 4),
                            Type = "Peer"
                        }).ToList();
                    result.AddRange(peerData);
                }

                return Ok(result);
            }

            // ─────────────────────────────────────────────────────────────────────
            // GET api/TeacherSelf/GetTeacherResultByCourse
            //     ?teacherId=x&courseCode=x&sessionId=x
            // ─────────────────────────────────────────────────────────────────────
            [HttpGet]
            [Route("GetTeacherResultByCourse")]
            public IHttpActionResult GetTeachersResultByCourse(
                string teacherId,
                string courseCode,
                int sessionId)
            {
                const double MAX = 4.0;
                const double SCALE = 10.0;

                var peerList = db.PeerEvaluation
                    .Where(p => p.evaluateeID == teacherId &&
                           (string.IsNullOrEmpty(courseCode) ||
                            p.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()) &&
                           p.PeerEvaluator.sessionID == sessionId)
                    .ToList();

                double peerTotal = peerList.Sum(p => (double)p.score);
                double peerMax = peerList.Count * MAX;
                double peerAvg = peerMax > 0 ? (peerTotal / peerMax) * SCALE : 0;

                var studentList = db.StudentEvaluation
                    .Where(s => s.Enrollment.teacherID == teacherId &&
                           s.Enrollment.sessionID == sessionId &&
                           (string.IsNullOrEmpty(courseCode) || s.Enrollment.courseCode == courseCode))
                    .ToList();

                double stuTotal = studentList.Sum(s => (double)s.score);
                double stuMax = studentList.Count * MAX;
                double stuAvg = stuMax > 0 ? (stuTotal / stuMax) * SCALE : 0;

                double totalScore = peerTotal + stuTotal;
                double totalMax = peerMax + stuMax;
                double overallPct = totalMax > 0 ? (totalScore / totalMax) * 100 : 0;

                var name = db.Teacher.Where(t => t.userID == teacherId).Select(t => t.name).FirstOrDefault();

                return Ok(new
                {
                    Name = name,
                    PeerAverage = Math.Round(peerAvg, 2),
                    StudentAverage = Math.Round(stuAvg, 2),
                    Percentage = Math.Round(overallPct, 2),
                    PeerTotal = peerTotal,
                    PeerMax = peerMax,
                    StudentTotal = stuTotal,
                    StudentMax = stuMax,
                    Total = totalScore,
                    TotalMax = totalMax
                });
            }

        // ─────────────────────────────────────────────────────────────────────
        // GET api/TeacherSelf/GetCourseQuestionDetail/{teacherId}/{sessionId}/{courseCode}
        //     ?evaluationType=both|student|peer
        //
        // ✅ Returns per-question detail WITH individual evaluator names & scores
        //    StudentDetails = list of { StudentName, RollNo, Score }
        //    For Peer type  = list of { StudentName (teacher name), RollNo (teacherID), Score }
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet]
        [Route("GetCourseQuestionDetail/{teacherId}/{sessionId}/{courseCode}")]
        public IHttpActionResult GetCourseQuestionDetail(string teacherId, int sessionId, string courseCode, string evaluationType = "both", string questionStatus = "all")
        {
            var result = new List<object>();
            bool isAllCourses = courseCode.ToUpper() == "ALL";
            bool onlyCritical = questionStatus.ToLower() == "critical";

            if (evaluationType == "student" || evaluationType == "both")
            {
                var studentData = db.StudentEvaluation
                    .Where(s => s.Enrollment.teacherID == teacherId
                                && s.Enrollment.sessionID == sessionId
                                && (isAllCourses || s.Enrollment.courseCode == courseCode)
                                // New Filter: Only include if question status matches
                                && (!onlyCritical || db.Questions.Any(q => q.QuestionID == s.questionID && q.isCritical == true)))
                    .GroupBy(s => s.questionID)
                    .Select(g => new
                    {
                        QuestionId = g.Key,
                        QuestionText = db.Questions.Where(q => q.QuestionID == g.Key).Select(q => q.QuestionText).FirstOrDefault(),
                        AverageScore = g.Average(x => (double?)x.score) ?? 0,
                        TotalResponses = g.Count(),
                        Score1 = g.Count(x => x.score == 1),
                        Score2 = g.Count(x => x.score == 2),
                        Score3 = g.Count(x => x.score == 3),
                        Score4 = g.Count(x => x.score == 4),
                        Type = "Student",
                        StudentDetails = g.Select(s => new { StudentName = s.Enrollment.Student.name, RollNo = s.Enrollment.studentID, Score = s.score }).ToList()
                    }).ToList();
                result.AddRange(studentData);
            }

            if (evaluationType == "peer" || evaluationType == "both")
            {
                var peerData = db.PeerEvaluation
                    .Where(p => p.evaluateeID == teacherId
                                && p.PeerEvaluator.sessionID == sessionId
                                && (isAllCourses || p.courseCode == courseCode)
                                // New Filter: Only include if question status matches
                                && (!onlyCritical || db.Questions.Any(q => q.QuestionID == p.questionID && q.isCritical == true)))
                    .GroupBy(p => p.questionID)
                    .Select(g => new
                    {
                        QuestionId = g.Key,
                        QuestionText = db.Questions.Where(q => q.QuestionID == g.Key).Select(q => q.QuestionText).FirstOrDefault(),
                        AverageScore = g.Average(x => (double?)x.score) ?? 0,
                        TotalResponses = g.Count(),
                        Score1 = g.Count(x => x.score == 1),
                        Score2 = g.Count(x => x.score == 2),
                        Score3 = g.Count(x => x.score == 3),
                        Score4 = g.Count(x => x.score == 4),
                        Type = "Peer",
                        StudentDetails = g.Select(p => new { StudentName = p.PeerEvaluator.Teacher.name, RollNo = p.PeerEvaluator.teacherID, Score = p.score }).ToList()
                    }).ToList();
                result.AddRange(peerData);
            }

            return Ok(result);
        }
    }
    }

