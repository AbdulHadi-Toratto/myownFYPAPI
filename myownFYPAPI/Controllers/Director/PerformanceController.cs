using myownFYPAPI.Models;
using myownFYPAPI.Models.DTO;
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

        [Route("GetDepartments")]
        public IHttpActionResult GetDepartments()
        {

            var departments = db.Teacher
                .Where(t => t.department != null && t.department != "")
                .Select(t => t.department)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            return Ok(departments);
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
        [Route("GetTeachersPerformanceList")]
        public IHttpActionResult GetTeachersPerformanceList(
     int sessionId,
     string department = "All",
     string courseCode = "All"
 )
        {
            var query = db.Enrollment
                .Where(e => e.sessionID == sessionId);

            // ✅ Department Filter
            if (department != "All")
            {
                query = query.Where(e => e.Teacher.department == department);
            }

            // ✅ Course Filter
            if (courseCode != "All")
            {
                query = query.Where(e => e.courseCode == courseCode);
            }

            // ✅ IMPORTANT:
            // Get EACH teacher-course pair
            var enrollments = query
                .Select(e => new
                {
                    TeacherID = e.teacherID,
                    CourseCode = e.courseCode
                })
                .Distinct()
                .ToList();

            var finalData = new List<object>();

            foreach (var item in enrollments)
            {
                var perf = CalculatePerformance(
                    item.TeacherID,
                    sessionId,
                    item.CourseCode
                );

                finalData.Add(perf);
            }

            return Ok(finalData);
        }

        // Helper method (Taake code duplicate na ho)
        private object CalculatePerformance(
      string teacherId,
      int sessionId,
      string courseCode
  )
        {
            const double MAX = 4.0;
            const double SCALE = 10.0;

            // =========================
            // STUDENT EVALUATION
            // =========================
            var studentList = db.StudentEvaluation
                .Where(s =>
                    s.Enrollment.teacherID == teacherId &&
                    s.Enrollment.sessionID == sessionId &&
                    s.Enrollment.courseCode == courseCode
                )
                .ToList();

            double sTotal = studentList.Sum(s => (double)s.score);

            double sMax = studentList.Count * MAX;

            double sAvg = sMax > 0
                ? (sTotal / sMax) * SCALE
                : 0;

            // =========================
            // PEER EVALUATION
            // =========================
            var peerList = db.PeerEvaluation
                .Where(p =>
                    p.evaluateeID == teacherId &&
                    p.courseCode == courseCode &&
                    p.SessionId == sessionId
                )
                .ToList();

            double pTotal = peerList.Sum(p => (double)p.score);

            double pMax = peerList.Count * MAX;

            double pAvg = pMax > 0
                ? (pTotal / pMax) * SCALE
                : 0;

            // =========================
            // CHR
            // =========================
            double chrAvg = 0.0;

            var chrRawData = db.CHR
                .Where(c =>
                    c.TeacherID == teacherId &&
                    c.sessionID == sessionId
                )
                .Select(x => new
                {
                    LateIn = x.LateIn ?? 0,
                    LeftEarly = x.LeftEarly ?? 0
                })
                .ToList();

            chrAvg = chrRawData.Any()
                ? chrRawData.Select(x =>
                {
                    int total = x.LateIn + x.LeftEarly;

                    if (total >= 10) return 0.0;

                    if (total >= 6) return 3.0;

                    if (total >= 1) return 4.0;

                    return 5.0;

                }).Average()
                : 0.0;

            double chrPerc = Math.Round((chrAvg / 5.0) * SCALE, 2);

            // =========================
            // TEACHER
            // =========================
            var teacher = db.Teacher
                .FirstOrDefault(t => t.userID == teacherId);

            return new
            {
                TeacherID = teacherId,

                Name = teacher?.name,

                CourseCode = courseCode,

                StudentAverage = Math.Round(sAvg, 2),

                PeerAverage = Math.Round(pAvg, 2),

                ChrAverage = chrPerc,

                ChrRawScore = Math.Round(chrAvg, 2),
            };
        }


        //[HttpGet]
        //[Route("GetTeachersPerformanceList")]
        //public IHttpActionResult GetTeachersPerformanceList(int sessionId, string department = "All", string courseCode = "All")
        //{
        //    var query = db.Enrollment.Where(e => e.sessionID == sessionId);

        //    if (department != "All") query = query.Where(e => e.Teacher.department == department);
        //    if (courseCode != "All") query = query.Where(e => e.courseCode == courseCode);

        //    var teacherIds = query.Select(e => e.teacherID).Distinct().ToList();
        //    var finalData = new List<object>();

        //    foreach (var tid in teacherIds)
        //    {
        //        // Yahan aap apna CalculatePerformance logic call karein
        //        // Isme ConfidentialEvaluation ka logic bhi add karein
        //        var perf = CalculatePerformance(tid, sessionId);
        //        finalData.Add(perf);
        //    }
        //    return Ok(finalData);
        //}

        //// Helper method (Taake code duplicate na ho)
        //private object CalculatePerformance(string teacherId, int sessionId)
        //{
        //    const double MAX = 4.0;
        //    const double SCALE = 10.0;

        //    // 1. Student Evaluations
        //    var studentList = db.StudentEvaluation
        //        .Where(s => s.Enrollment.teacherID == teacherId && s.Enrollment.sessionID == sessionId)
        //        .ToList();
        //    double sTotal = studentList.Sum(s => (double)s.score);
        //    double sMax = studentList.Count * MAX;
        //    double sAvg = sMax > 0 ? (sTotal / sMax) * SCALE : 0;

        //    // 2. Peer Evaluations
        //    var peerList = db.PeerEvaluation
        //        .Where(p => p.evaluateeID == teacherId && p.PeerEvaluator.sessionID == sessionId)
        //        .ToList();
        //    double pTotal = peerList.Sum(p => (double)p.score);
        //    double pMax = peerList.Count * MAX;
        //    double pAvg = pMax > 0 ? (pTotal / pMax) * SCALE : 0;

        //    // ✅ 3. CHR — Enrollment se session verify
        //    var isEnrolled = db.Enrollment
        //        .Any(e => e.teacherID == teacherId && e.sessionID == sessionId);

        //    // 3. CHR Average Score — Session filter ke saath
        //    // Sirf us session ki CHR records consider hongi
        //    var chrAvg = 0.0;

        //    var chrRawData = db.CHR
        //        .Where(c => c.TeacherID == teacherId && c.sessionID == sessionId)
        //        .Select(x => new { LateIn = x.LateIn ?? 0, LeftEarly = x.LeftEarly ?? 0 })
        //        .ToList();

        //    chrAvg = chrRawData.Any()
        //        ? chrRawData.Select(x => {
        //            int total = x.LateIn + x.LeftEarly;
        //            if (total >= 10) return 0.0;
        //            if (total >= 6) return 3.0;
        //            if (total >= 1) return 4.0;
        //            return 5.0;
        //        }).Average()
        //        : 0.0;

        //    // CHR ko 10 scale pe convert karo (baaki scores ki tarah)
        //    double chrPerc = Math.Round((chrAvg / 5.0) * SCALE, 2);

        //    var teacher = db.Teacher.FirstOrDefault(t => t.userID == teacherId);

        //    return new
        //    {
        //        TeacherID = teacherId,
        //        Name = teacher?.name,
        //        StudentAverage = Math.Round(sAvg, 2),
        //        PeerAverage = Math.Round(pAvg, 2),
        //        ChrAverage = chrPerc,           // ✅ CHR score 0-10 scale
        //        ChrRawScore = Math.Round(chrAvg, 2), // ✅ Raw score 0-5 scale
        //        CourseCode = db.Enrollment
        //            .Where(e => e.teacherID == teacherId && e.sessionID == sessionId)
        //            .Select(e => e.courseCode).FirstOrDefault()
        //    };
        //}

        //old KPI endpoint - not used yet
        //[HttpGet]
        //[Route("GetTeacherPerformance")]
        //public IHttpActionResult GetTeacherPerformance(int sessionId, string department = null, string courseCode = null)
        //{
        //    var query = db.Enrollment.Where(e => e.sessionID == sessionId);

        //    if (!string.IsNullOrEmpty(courseCode) && courseCode != "All")
        //    {
        //        query = query.Where(e => e.courseCode == courseCode);
        //    }

        //    var data = query
        //        .GroupBy(e => new { e.teacherID, e.courseCode })
        //        .Select(g => new
        //        {
        //            TeacherID = g.Key.teacherID,
        //            CourseCode = g.Key.courseCode,

        //            TeacherName = db.Teacher
        //                .Where(t => t.userID == g.Key.teacherID)
        //                .Select(t => t.name)
        //                .FirstOrDefault(),

        //            Department = db.Teacher
        //                .Where(t => t.userID == g.Key.teacherID)
        //                .Select(t => t.department)
        //                .FirstOrDefault(),

        //            // ✅ Peer Evaluation Avg
        //            PeerAvg = db.PeerEvaluation
        //                .Where(p =>
        //                    p.evaluateeID == g.Key.teacherID &&
        //                    p.courseCode == g.Key.courseCode &&
        //                    p.SessionId == sessionId
        //                )
        //                .Average(p => (int?)p.score),

        //            // ✅ Student Evaluation Avg (FIXED JOIN)
        //            StudentAvg = db.StudentEvaluation
        //                .Where(s =>
        //                    s.SessionID == sessionId &&
        //                    s.Enrollment.teacherID == g.Key.teacherID &&
        //                    s.Enrollment.courseCode == g.Key.courseCode
        //                )
        //                .Average(s => (int?)s.score)
        //        })
        //        .ToList();

        //    // ✅ APPLY DEPARTMENT FILTER
        //    if (!string.IsNullOrEmpty(department))
        //    {
        //        data = data.Where(d => d.Department == department).ToList();
        //    }

        //    // ✅ FINAL RESULT WITH COMBINED AVERAGE
        //    var result = data.Select(x =>
        //    {
        //        var peer = x.PeerAvg ?? 0;
        //        var student = x.StudentAvg ?? 0;

        //        int count = 0;
        //        if (x.PeerAvg != null) count++;
        //        if (x.StudentAvg != null) count++;

        //        var finalAvg = count > 0 ? (peer + student) / count : 0;

        //        return new
        //        {
        //            x.TeacherID,
        //            x.TeacherName,
        //            x.CourseCode,
        //            x.Department,
        //            Percentage = (finalAvg / 4.0) * 100
        //        };
        //    });

        //    return Ok(result);
        //}

        [HttpGet]
        [Route("GetAllCourses")]
        public IHttpActionResult GetAllCourses()
        {
            var courses = db.Course
                .Select(c => c.code)
                .Distinct()
                .ToList();

            return Ok(courses);
        }

        [HttpPost]
        [Route("CompareTeachers")]
        public IHttpActionResult CompareTeachers(CompareDTO dto)
        {
            var result = new List<object>();

            if (dto.mode == "course")
            {
                // ✅ Get latest session for this course
                var latestSessionId = db.Enrollment
                    .Where(e => e.courseCode == dto.courseCode)
                    .OrderByDescending(e => e.sessionID)
                    .Select(e => e.sessionID)
                    .FirstOrDefault();

                result.Add(GetTeacherScore(dto.teacherA, dto.courseCode, latestSessionId));
                result.Add(GetTeacherScore(dto.teacherB, dto.courseCode, latestSessionId));
            }
            else
            {
                result.Add(GetTeacherScore(dto.teacherA, null, dto.session1));
                result.Add(GetTeacherScore(dto.teacherA, null, dto.session2));
            }

            return Ok(result);
        }

        private object GetTeacherScore(string teacherId, string courseCode, int? sessionId)
        {
            const double MAX_SCORE_PER_QUESTION = 4.0;  // Each question max points
            const double SCALE_TO_TEN = 10.0;           // Average score scaled out of 10

            // --- Peer Evaluation ---
            var peerList = db.PeerEvaluation
                .Where(p => p.evaluateeID == teacherId &&
                       (courseCode == null || p.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()) &&
                       (sessionId == null || p.SessionId == sessionId))
                .ToList();

            double peerTotalScore = peerList.Sum(p => (double)p.score);
            double peerMaxTotal = peerList.Count * MAX_SCORE_PER_QUESTION;
            double peerAverageOutOfTen = peerMaxTotal > 0 ? (peerTotalScore / peerMaxTotal) * SCALE_TO_TEN : 0;

            // --- Student Evaluation ---
            var studentList = db.StudentEvaluation
                .Where(s =>
                    (courseCode == null || s.Enrollment.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()) &&
                    (sessionId == null || s.SessionID == sessionId) &&
                    s.Enrollment.teacherID == teacherId
                )
                .ToList();

            double studentTotalScore = studentList.Sum(s => (double)s.score);
            double studentMaxTotal = studentList.Count * MAX_SCORE_PER_QUESTION;
            double studentAverageOutOfTen = studentMaxTotal > 0 ? (studentTotalScore / studentMaxTotal) * SCALE_TO_TEN : 0;

            // --- Overall Average Out of 100 ---
            double overallAveragePercentage = 0;
            double totalScore = peerTotalScore + studentTotalScore;
            double totalMax = peerMaxTotal + studentMaxTotal;
            if (totalMax > 0)
                overallAveragePercentage = (totalScore / totalMax) * 100;

            // --- Teacher Name ---
            var name = db.Teacher
                .Where(t => t.userID == teacherId)
                .Select(t => t.name)
                .FirstOrDefault();

            return new
            {
                Name = name,
                PeerAverageOutOfTen = Math.Round(peerAverageOutOfTen, 2),
                StudentAverageOutOfTen = Math.Round(studentAverageOutOfTen, 2),
                OverallAverageOutOfHundred = Math.Round(overallAveragePercentage, 2),
                PeerTotalScore = Math.Round(peerTotalScore, 2),
                PeerMaxTotal = peerMaxTotal,
                StudentTotalScore = Math.Round(studentTotalScore, 2),
                StudentMaxTotal = studentMaxTotal,
                TotalScore = Math.Round(totalScore, 2),
                TotalMax = totalMax,
            };
        }

        [HttpGet]
        [Route("GetAllTeachers")]
        public IHttpActionResult GetAllTeachers()
        {
            var teachers = db.Enrollment
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
        [Route("GetTeacherQuestionStatsFull")]
        public IHttpActionResult GetTeacherQuestionStatsFull(string teacherId, int sessionId, string evaluationType, string courseCode = null)
        {
            var result = new List<object>();

            // =========================
            // 🔹 STUDENT EVALUATION
            // =========================
            if (evaluationType == "student" || evaluationType == "both")
            {
                var studentQuery = db.StudentEvaluation
                    .Where(s =>
                        s.SessionID == sessionId &&
                        s.Enrollment.teacherID == teacherId &&
                        (courseCode == null || s.Enrollment.courseCode == courseCode)
                    );

                var studentData = studentQuery
                    .GroupBy(s => s.questionID)
                    .Select(g => new
                    {
                        QuestionId = g.Key,

                        QuestionText = db.Questions
                            .Where(q => q.QuestionID == g.Key)
                            .Select(q => q.QuestionText)
                            .FirstOrDefault(),

                        AverageScore = g.Average(x => (double?)x.score) ?? 0,

                        TotalResponses = g.Count(),

                        Score1 = g.Count(x => x.score == 1),
                        Score2 = g.Count(x => x.score == 2),
                        Score3 = g.Count(x => x.score == 3),
                        Score4 = g.Count(x => x.score == 4),

                        Type = "Student"
                    })
                    .ToList();

                result.AddRange(studentData);
            }

            // =========================
            // 🔹 PEER EVALUATION
            // =========================
            if (evaluationType == "peer" || evaluationType == "both")
            {
                var peerQuery = db.PeerEvaluation
                    .Where(p =>
                        p.SessionId == sessionId &&
                        p.evaluateeID == teacherId &&
                        (courseCode == null || p.courseCode == courseCode)
                    );

                var peerData = peerQuery
                    .GroupBy(p => p.questionID)
                    .Select(g => new
                    {
                        QuestionId = g.Key,

                        QuestionText = db.Questions
                            .Where(q => q.QuestionID == g.Key)
                            .Select(q => q.QuestionText)
                            .FirstOrDefault(),

                        AverageScore = g.Average(x => (double?)x.score) ?? 0,

                        TotalResponses = g.Count(),

                        Score1 = g.Count(x => x.score == 1),
                        Score2 = g.Count(x => x.score == 2),
                        Score3 = g.Count(x => x.score == 3),
                        Score4 = g.Count(x => x.score == 4),

                        Type = "Peer"
                    })
                    .ToList();

                result.AddRange(peerData);
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("GetTeacherResultByCourse")]
        public IHttpActionResult GetTeachersResultByCourse(string teacherId, string courseCode, int sessionId)
        {
            const double MAX_SCORE = 4.0;
            const double SCALE_TO_TEN = 10.0;

            // --- PEER ---
            var peerList = db.PeerEvaluation
                .Where(p => p.evaluateeID == teacherId &&
                       (string.IsNullOrEmpty(courseCode) || p.courseCode.Trim().ToUpper() == courseCode.Trim().ToUpper()) &&
                       p.SessionId == sessionId)
                .ToList();

            double peerTotal = peerList.Sum(x => (double)x.score);
            double peerMax = peerList.Count * MAX_SCORE;
            double peerAvg = peerMax > 0 ? (peerTotal / peerMax) * SCALE_TO_TEN : 0;

            // --- STUDENT ---
            var studentList = db.StudentEvaluation
                .Where(s =>
                    (string.IsNullOrEmpty(courseCode) || s.Enrollment.courseCode.Trim().ToUpper() == courseCode.Trim().ToUpper()) &&
                    s.SessionID == sessionId &&
                    s.Enrollment.teacherID == teacherId)
                .ToList();

            double studentTotal = studentList.Sum(x => (double)x.score);
            double studentMax = studentList.Count * MAX_SCORE;
            double studentAvg = studentMax > 0 ? (studentTotal / studentMax) * SCALE_TO_TEN : 0;

            // --- SOCIETY ---
            var societyList = db.SocietyEvaluation
                .Where(s => s.EvaluateeId == teacherId && s.SessionId == sessionId)
                .ToList();

            double societyTotal = societyList.Sum(x => (double)x.Score);
            double societyMax = societyList.Count * MAX_SCORE;
            double societyAvg = societyMax > 0 ? (societyTotal / societyMax) * SCALE_TO_TEN : 0;

            // --- COURSE MANAGEMENT (KPI SCORE) ---
            var courseMgmtList = (
                from score in db.KPIScore
                join empKpi in db.EmployeSessionKPI on score.empKPIID equals empKpi.id
                where score.empID == teacherId && empKpi.SessionID == sessionId
                select score.score
            ).ToList();

            double courseMgmtTotal = courseMgmtList.Sum(x => (double)x);
            double courseMgmtMax = courseMgmtList.Count * MAX_SCORE;
            double courseMgmtAvg = courseMgmtMax > 0 ? (courseMgmtTotal / courseMgmtMax) * SCALE_TO_TEN : 0;

            // --- TOTAL (SIMPLE SUM LIKE BEFORE) ---
            double total = peerTotal + studentTotal + societyTotal + courseMgmtTotal;
            double totalMax = peerMax + studentMax + societyMax + courseMgmtMax;

            double percentage = totalMax > 0 ? (total / totalMax) * 100 : 0;

            // --- NAME ---
            var name = db.Teacher
                .Where(t => t.userID == teacherId)
                .Select(t => t.name)
                .FirstOrDefault();

            return Ok(new
            {
                Name = name,

                // Averages (for chart)
                PeerAverage = Math.Round(peerAvg, 2),
                StudentAverage = Math.Round(studentAvg, 2),
                SocietyAverage = Math.Round(societyAvg, 2),
                CourseMgmtAverage = Math.Round(courseMgmtAvg, 2),

                // Totals (same structure as before)
                PeerTotal = peerTotal,
                PeerMax = peerMax,

                StudentTotal = studentTotal,
                StudentMax = studentMax,

                SocietyTotal = societyTotal,
                SocietyMax = societyMax,

                CourseMgmtTotal = courseMgmtTotal,
                CourseMgmtMax = courseMgmtMax,

                // Final
                Total = total,
                TotalMax = totalMax,
                Percentage = Math.Round(percentage, 2)
            });
        }

        //[HttpGet]
        //[Route("GetTeacherResultByCourse")]
        //public IHttpActionResult GetTeachersResultByCourse(string teacherId, string courseCode, int sessionId)
        //{
        //    const double MAX_SCORE_PER_QUESTION = 4.0;
        //    const double SCALE_TO_TEN = 10.0;

        //    // --- Peer Evaluation ---
        //    var peerList = db.PeerEvaluation
        //        .Where(p => p.evaluateeID == teacherId &&
        //               (courseCode == null || p.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()) &&
        //               (sessionId == null || p.SessionId == sessionId))
        //        .ToList();

        //    double peerTotalScore = peerList.Sum(p => (double)p.score);
        //    double peerMaxTotal = peerList.Count * MAX_SCORE_PER_QUESTION;
        //    double peerAverageOutOfTen = peerMaxTotal > 0
        //        ? (peerTotalScore / peerMaxTotal) * SCALE_TO_TEN
        //        : 0;

        //    // --- Student Evaluation ---
        //    var studentList = db.StudentEvaluation
        //        .Where(s =>
        //            (courseCode == null || s.Enrollment.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()) &&
        //            (sessionId == null || s.SessionID == sessionId) &&
        //            s.Enrollment.teacherID == teacherId
        //        )
        //        .ToList();

        //    double studentTotalScore = studentList.Sum(s => (double)s.score);
        //    double studentMaxTotal = studentList.Count * MAX_SCORE_PER_QUESTION;
        //    double studentAverageOutOfTen = studentMaxTotal > 0
        //        ? (studentTotalScore / studentMaxTotal) * SCALE_TO_TEN
        //        : 0;

        //    // --- Overall ---
        //    double totalScore = peerTotalScore + studentTotalScore;
        //    double totalMax = peerMaxTotal + studentMaxTotal;

        //    double overallPercentage = totalMax > 0
        //        ? (totalScore / totalMax) * 100
        //        : 0;

        //    var name = db.Teacher
        //        .Where(t => t.userID == teacherId)
        //        .Select(t => t.name)
        //        .FirstOrDefault();

        //    return Ok(new
        //    {
        //        Name = name,
        //        PeerAverage = Math.Round(peerAverageOutOfTen, 2),
        //        StudentAverage = Math.Round(studentAverageOutOfTen, 2),
        //        Percentage = Math.Round(overallPercentage, 2),

        //        PeerTotal = peerTotalScore,
        //        PeerMax = peerMaxTotal,

        //        StudentTotal = studentTotalScore,
        //        StudentMax = studentMaxTotal,

        //        Total = totalScore,
        //        TotalMax = totalMax
        //    });
        //}


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


        [HttpGet]
        [Route("GetTeacherPerformanceAnalytics/{teacherId}/{sessionId}")]
        public IHttpActionResult GetTeacherPerformanceAnalytics(string teacherId, int sessionId, int? kpiId = null)
        {
            try
            {
                // 1. Session + Teacher
                var currentSession = db.Session.FirstOrDefault(s => s.id == sessionId);
                if (currentSession == null) return BadRequest("Invalid Session ID.");

                var teacherData = db.Teacher.FirstOrDefault(t => t.userID == teacherId);
                if (teacherData == null) return BadRequest("Teacher not found.");

                // ================= SOCIETY CHECK /// same project =================
                var isSocietyMember = db.SocietyAssignments
                    .Any(sa => sa.TeacherId == teacherId && sa.SessionId == sessionId);


                // 2. Active KPIs
                //var activeKPIs = db.EmployeSessionKPI
                //    .Where(esk => esk.SessionID == sessionId)
                //    .Select(esk => new
                //    {
                //        esk.id,
                //        esk.KPIID,
                //        esk.SubKPIID,
                //        KPIName = db.KPI.Where(k => k.id == esk.KPIID).Select(k => k.name).FirstOrDefault(),
                //        SubKPIName = db.SubKPI.Where(sk => sk.id == esk.SubKPIID).Select(sk => sk.name).FirstOrDefault()
                //    })
                //    .ToList();

                // ✅ empTypeId filter add kiya
                var activeKPIs = db.EmployeSessionKPI
                    .Where(esk => esk.SessionID == sessionId &&
                          (kpiId == null || esk.KPIID == kpiId)) // ✅ Direct KPIID check
                    .Select(esk => new
                    {
                        esk.id,
                        esk.KPIID,
                        esk.SubKPIID,
                        KPIName = db.KPI.Where(k => k.id == esk.KPIID).Select(k => k.name).FirstOrDefault(),
                        SubKPIName = db.SubKPI.Where(sk => sk.id == esk.SubKPIID).Select(sk => sk.name).FirstOrDefault()
                    })
                    .ToList();

                if (!activeKPIs.Any())
                    return Ok(new { Status = "Empty", Message = "No KPIs configured for this session." });

                // ================= FILTER SOCIETY KPI =================
                activeKPIs = activeKPIs.Where(item =>
                {
                    string subName = (item.SubKPIName ?? "").ToLower();

                    if (subName.Contains("society") && !isSocietyMember)
                        return false;
                    ////else project specific KPIs can also be filtered here if needed by checking subName for certain keywords and validating against teacher's involvement in those projects
                    return true;
                }).ToList();

                // 3. Averages
                var studentAvg = db.StudentEvaluation
                    .Where(se => se.Enrollment.teacherID == teacherId && se.Enrollment.sessionID == sessionId)
                    .Select(x => (double?)x.score)
                    .DefaultIfEmpty()
                    .Average() ?? 0;

                var peerAvg = db.PeerEvaluation
                    .Where(pe => pe.evaluateeID == teacherId && pe.PeerEvaluator.sessionID == sessionId)
                    .Select(x => (double?)x.score)
                    .DefaultIfEmpty()
                    .Average() ?? 0;

                var societyAvg = db.SocietyEvaluation
                    .Where(se => se.EvaluateeId == teacherId && se.SessionId == sessionId)
                    .Select(x => (double?)x.Score)
                    .DefaultIfEmpty()
                    .Average() ?? 0;////same project

                // 3. CHR Average Score — Session filter ke saath
                // Sirf us session ki CHR records consider hongi
                var chrAvg = 0.0;

                var chrRawData = db.CHR
                    .Where(c => c.TeacherID == teacherId && c.sessionID == sessionId)
                    .Select(x => new { LateIn = x.LateIn ?? 0, LeftEarly = x.LeftEarly ?? 0 })
                    .ToList();

                chrAvg = chrRawData.Any()
                    ? chrRawData.Select(x => {
                        int total = x.LateIn + x.LeftEarly;
                        if (total >= 10) return 0.0;
                        if (total >= 6) return 3.0;
                        if (total >= 1) return 4.0;
                        return 5.0;
                    }).Average()
                    : 0.0;

                var confScores = db.KPIScore
                    .Where(ks => ks.empID == teacherId && ks.EmployeSessionKPI.SessionID == sessionId)
                    .ToList();



                // 4. Breakdown
                var groupedKPIs = activeKPIs.GroupBy(k => new { k.KPIID, k.KPIName });

                var finalBreakdown = new List<object>();

                double totalAchieved = 0;
                double totalWeight = 0;

                foreach (var kpiGroup in groupedKPIs)
                {
                    var subDetails = new List<object>();
                    double kpiAchieved = 0;
                    double kpiWeight = 0;

                    foreach (var item in kpiGroup)
                    {
                        var weightEntry = db.SessionKPIWeight.FirstOrDefault(w =>
                            w.SessionID == sessionId &&
                            w.KPIID == item.KPIID &&
                            w.SubKPIID == item.SubKPIID);

                        double weight = weightEntry?.Weight ?? 0;
                        string subName = (item.SubKPIName ?? "").ToLower();

                        double multiplier = 0;
                        double maxScale = 4.0;

                        // ================= SCORE LOGIC =================
                        if (subName.Contains("student") || subName.Contains("Student Evalution"))
                        {
                            multiplier = studentAvg;
                        }
                        else if (subName.Contains("peer") || subName.Contains("Peer Evalution"))
                        {
                            multiplier = peerAvg;
                        }
                        else if (subName.Contains("society") || subName.Contains("Society Management"))
                        {
                            multiplier = isSocietyMember ? societyAvg : 0;
                        }
                        else if (subName.Contains("confidential") || subName.Contains("Confidential Evalution"))
                        {
                            multiplier = 0;
                        }
                        else if (subName.Contains("chr") || subName.Contains("CHR") || subName.Contains("class held report"))
                        {
                            multiplier = chrAvg;   // ← CHR score 0-5
                            maxScale = 5.0;
                        }
                        else
                        {
                            var specificScore = confScores
                                .Where(cs => cs.empKPIID == item.id)
                                .Average(cs => (double?)cs.score);

                            multiplier = specificScore ?? 0;
                            maxScale = 5.0;
                        }

                        double achieved = Math.Round((multiplier / maxScale) * weight, 2);

                        subDetails.Add(new
                        {
                            SubName = item.SubKPIName,
                            SubMax = weight,
                            SubAchieved = achieved,
                            MaxScale = maxScale,
                            RawScore = multiplier,
                            IsSociety = subName.Contains("society") || subName.Contains("society Management") && isSocietyMember,
                            IsCHR = subName.Contains("chr") || subName.Contains("CHR") || subName.Contains("class held report")

                        });

                        kpiAchieved += achieved;
                        kpiWeight += weight;
                    }

                    finalBreakdown.Add(new
                    {
                        KPIName = kpiGroup.Key.KPIName,
                        KPIWeight = kpiWeight,
                        KPIAchieved = Math.Round(kpiAchieved, 2),
                        SubDetails = subDetails
                    });

                    totalAchieved += kpiAchieved;
                    totalWeight += kpiWeight;
                }

                // 5. FINAL SCORE
                double overallPercentage = totalWeight > 0
                    ? Math.Round((totalAchieved / totalWeight) * 100, 2)
                    : 0;

                // 6. RESPONSE
                return Ok(new
                {
                    Status = "Success",
                    TeacherName = teacherData?.name,
                    Department = teacherData?.department,
                    SessionName = currentSession.name,
                    IsSocietyMember = isSocietyMember,
                    OverallPercentage = overallPercentage,
                    ChrAvgScore = Math.Round(chrAvg, 2),
                    Breakdown = finalBreakdown
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]

        [Route("GetKpiTypesBySession/{sessionId}")]
        public IHttpActionResult GetKpiTypesBySession(int sessionId)
        {
            var types = db.EmployeSessionKPI
                .Where(esk => esk.SessionID == sessionId)
                .Select(esk => new {
                    id = esk.KPIID,                    // ✅ EmployeetypeID ki jagah KPIID
                    name = db.KPI
                        .Where(k => k.id == esk.KPIID) // ✅ KPI table se naam
                        .Select(k => k.name)
                        .FirstOrDefault()
                })
                .Distinct()
                .ToList()
                .GroupBy(x => x.id)                    // ✅ Duplicate KPIs hata
                .Select(g => new { id = g.Key, name = g.First().name })
                .ToList();

            return Ok(types);
        }


        //[HttpGet]
        //[Route("GetTeacherPerformanceAnalytics/{teacherId}/{sessionId}")]
        //public IHttpActionResult GetTeacherPerformanceAnalytics(string teacherId, int sessionId)
        //{
        //    try
        //    {
        //        // 1. Session + Teacher
        //        var currentSession = db.Session.FirstOrDefault(s => s.id == sessionId);
        //        if (currentSession == null) return BadRequest("Invalid Session ID.");

        //        var teacherData = db.Teacher.FirstOrDefault(t => t.userID == teacherId);
        //        if (teacherData == null) return BadRequest("Teacher not found.");

        //        // ================= SOCIETY CHECK /// same project =================
        //        var isSocietyMember = db.SocietyAssignments
        //            .Any(sa => sa.TeacherId == teacherId && sa.SessionId == sessionId);


        //        // 2. Active KPIs
        //        var activeKPIs = db.EmployeSessionKPI
        //            .Where(esk => esk.SessionID == sessionId)
        //            .Select(esk => new
        //            {
        //                esk.id,
        //                esk.KPIID,
        //                esk.SubKPIID,
        //                KPIName = db.KPI.Where(k => k.id == esk.KPIID).Select(k => k.name).FirstOrDefault(),
        //                SubKPIName = db.SubKPI.Where(sk => sk.id == esk.SubKPIID).Select(sk => sk.name).FirstOrDefault()
        //            })
        //            .ToList();

        //        if (!activeKPIs.Any())
        //            return Ok(new { Status = "Empty", Message = "No KPIs configured for this session." });

        //        // ================= FILTER SOCIETY KPI =================
        //        activeKPIs = activeKPIs.Where(item =>
        //        {
        //            string subName = (item.SubKPIName ?? "").ToLower();

        //            if (subName.Contains("society") && !isSocietyMember)
        //                return false;
        //            ////else project specific KPIs can also be filtered here if needed by checking subName for certain keywords and validating against teacher's involvement in those projects
        //            return true;
        //        }).ToList();

        //        // 3. Averages
        //        var studentAvg = db.StudentEvaluation
        //            .Where(se => se.Enrollment.teacherID == teacherId && se.Enrollment.sessionID == sessionId)
        //            .Select(x => (double?)x.score)
        //            .DefaultIfEmpty()
        //            .Average() ?? 0;

        //        var peerAvg = db.PeerEvaluation
        //            .Where(pe => pe.evaluateeID == teacherId && pe.PeerEvaluator.sessionID == sessionId)
        //            .Select(x => (double?)x.score)
        //            .DefaultIfEmpty()
        //            .Average() ?? 0;

        //        var societyAvg = db.SocietyEvaluation
        //            .Where(se => se.EvaluateeId == teacherId && se.SessionId == sessionId)
        //            .Select(x => (double?)x.Score)
        //            .DefaultIfEmpty()
        //            .Average() ?? 0;////same project

        //        // 3. CHR Average Score — Session filter ke saath
        //        // Sirf us session ki CHR records consider hongi
        //        var chrAvg = 0.0;

        //        var chrRawData = db.CHR
        //            .Where(c => c.TeacherID == teacherId && c.sessionID == sessionId)
        //            .Select(x => new { LateIn = x.LateIn ?? 0, LeftEarly = x.LeftEarly ?? 0 })
        //            .ToList();

        //        chrAvg = chrRawData.Any()
        //            ? chrRawData.Select(x => {
        //                int total = x.LateIn + x.LeftEarly;
        //                if (total >= 10) return 0.0;
        //                if (total >= 6) return 3.0;
        //                if (total >= 1) return 4.0;
        //                return 5.0;
        //            }).Average()
        //            : 0.0;

        //        var confScores = db.KPIScore
        //            .Where(ks => ks.empID == teacherId && ks.EmployeSessionKPI.SessionID == sessionId)
        //            .ToList();



        //        // 4. Breakdown
        //        var groupedKPIs = activeKPIs.GroupBy(k => new { k.KPIID, k.KPIName });

        //        var finalBreakdown = new List<object>();

        //        double totalAchieved = 0;
        //        double totalWeight = 0;

        //        foreach (var kpiGroup in groupedKPIs)
        //        {
        //            var subDetails = new List<object>();
        //            double kpiAchieved = 0;
        //            double kpiWeight = 0;

        //            foreach (var item in kpiGroup)
        //            {
        //                var weightEntry = db.SessionKPIWeight.FirstOrDefault(w =>
        //                    w.SessionID == sessionId &&
        //                    w.KPIID == item.KPIID &&
        //                    w.SubKPIID == item.SubKPIID);

        //                double weight = weightEntry?.Weight ?? 0;
        //                string subName = (item.SubKPIName ?? "").ToLower();

        //                double multiplier = 0;
        //                double maxScale = 4.0;

        //                // ================= SCORE LOGIC =================
        //                if (subName.Contains("student")|| subName.Contains("Student Evaluation"))
        //                {
        //                    multiplier = studentAvg;
        //                }
        //                else if (subName.Contains("peer") || subName.Contains("Peer Evaluation"))
        //                {
        //                    multiplier = peerAvg;
        //                }
        //                else if (subName.Contains("society") || subName.Contains("Society Management"))
        //                {
        //                    multiplier = isSocietyMember ? societyAvg : 0;
        //                }
        //                else if (subName.Contains("confidential") || subName.Contains("Confidential Evaluation"))
        //                {
        //                    multiplier = 0;
        //                }
        //                else if (subName.Contains("chr") || subName.Contains("CHR") || subName.Contains("class held report"))
        //                {
        //                    multiplier = chrAvg;   // ← CHR score 0-5
        //                    maxScale = 5.0;
        //                }
        //                else
        //                {
        //                    var specificScore = confScores
        //                        .Where(cs => cs.empKPIID == item.id)
        //                        .Average(cs => (double?)cs.score);

        //                    multiplier = specificScore ?? 0;
        //                    maxScale = 5.0;
        //                }

        //                double achieved = Math.Round((multiplier / maxScale) * weight, 2);

        //                subDetails.Add(new
        //                {
        //                    SubName = item.SubKPIName,
        //                    SubMax = weight,
        //                    SubAchieved = achieved,
        //                    MaxScale = maxScale,
        //                    RawScore = multiplier,
        //                    IsSociety = subName.Contains("society") ||subName.Contains("society Management") && isSocietyMember,
        //                    IsCHR = subName.Contains("chr") || subName.Contains("CHR") || subName.Contains("class held report")

        //                });

        //                kpiAchieved += achieved;
        //                kpiWeight += weight;
        //            }

        //            finalBreakdown.Add(new
        //            {
        //                KPIName = kpiGroup.Key.KPIName,
        //                KPIWeight = kpiWeight,
        //                KPIAchieved = Math.Round(kpiAchieved, 2),
        //                SubDetails = subDetails
        //            });

        //            totalAchieved += kpiAchieved;
        //            totalWeight += kpiWeight;
        //        }

        //        // 5. FINAL SCORE
        //        double overallPercentage = totalWeight > 0
        //            ? Math.Round((totalAchieved / totalWeight) * 100, 2)
        //            : 0;

        //        // 6. RESPONSE
        //        return Ok(new
        //        {
        //            Status = "Success",
        //            TeacherName = teacherData?.name,
        //            Department = teacherData?.department,
        //            SessionName = currentSession.name,
        //            IsSocietyMember = isSocietyMember,
        //            OverallPercentage = overallPercentage,
        //            ChrAvgScore = Math.Round(chrAvg, 2),
        //            Breakdown = finalBreakdown
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return InternalServerError(ex);
        //    }
        //}
    


      
    }
}

