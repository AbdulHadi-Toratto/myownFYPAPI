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
            const double MAX_SCORE = 4.0;

            // --- STEP 1: Get KPI Weights ---
            var weights = db.SessionKPIWeight
                .Where(w => w.SessionID == sessionId)
                .ToList();

            // Map weights (adjust names/IDs according to your DB)
            double peerWeight = weights
                .Where(w => w.KPIID == 1) // Peer KPI ID
                .Select(w => (double?)w.Weight)
                .FirstOrDefault() ?? 0;

            double studentWeight = weights
                .Where(w => w.KPIID == 2) // Student KPI ID
                .Select(w => (double?)w.Weight)
                .FirstOrDefault() ?? 0;

            double societyWeight = weights
                .Where(w => w.KPIID == 3) // Society KPI ID
                .Select(w => (double?)w.Weight)
                .FirstOrDefault() ?? 0;

            double courseMgmtWeight = weights
                .Where(w => w.SubKPIID != null) // Course Mgmt via SubKPI
                .Select(w => (double?)w.Weight)
                .FirstOrDefault() ?? 0;

            double totalWeight = peerWeight + studentWeight + societyWeight + courseMgmtWeight;

            // --- STEP 2: Base Query (Enrollment) ---
            var baseQuery = db.Enrollment
                .Where(e => e.sessionID == sessionId);

            if (!string.IsNullOrEmpty(courseCode) && courseCode != "All")
                baseQuery = baseQuery.Where(e => e.courseCode == courseCode);

            var grouped = baseQuery
                .GroupBy(e => new { e.teacherID, e.courseCode })
                .Select(g => new
                {
                    TeacherID = g.Key.teacherID,
                    CourseCode = g.Key.courseCode
                })
                .ToList();

            // --- STEP 3: Preload Teachers ---
            var teacherIds = grouped.Select(g => g.TeacherID).Distinct().ToList();

            var teachers = db.Teacher
                .Where(t => teacherIds.Contains(t.userID))
                .ToDictionary(t => t.userID, t => new { t.name, t.department });

            // --- STEP 4: Peer Evaluation ---
            var peerData = db.PeerEvaluation
                .Where(p => p.SessionId == sessionId)
                .GroupBy(p => new { p.evaluateeID, p.courseCode })
                .Select(g => new
                {
                    g.Key.evaluateeID,
                    g.Key.courseCode,
                    Avg = g.Average(x => (double?)x.score)
                })
                .ToList();

            // --- STEP 5: Student Evaluation ---
            var studentData = db.StudentEvaluation
                .Where(s => s.SessionID == sessionId)
                .GroupBy(s => new { s.Enrollment.teacherID, s.Enrollment.courseCode })
                .Select(g => new
                {
                    teacherID = g.Key.teacherID,
                    courseCode = g.Key.courseCode,
                    Avg = g.Average(x => (double?)x.score)
                })
                .ToList();

            // --- STEP 6: Society Evaluation ---
            var societyData = db.SocietyEvaluation
                .Where(s => s.SessionId == sessionId)
                .GroupBy(s => s.EvaluateeId)
                .Select(g => new
                {
                    teacherID = g.Key,
                    Avg = g.Average(x => (double?)x.Score)
                })
                .ToList();

            // --- STEP 7: Course Management (KPIScore) ---
            var courseMgmtData = (
                from score in db.KPIScore
                join empKpi in db.EmployeSessionKPI on score.empKPIID equals empKpi.id
                where empKpi.SessionID == sessionId
                group score by score.empID into g
                select new
                {
                    teacherID = g.Key,
                    Avg = g.Average(x => (double?)x.score)
                }
            ).ToList();

            // --- STEP 8: Combine Everything ---
            var result = grouped.Select(g =>
            {
                var teacher = teachers.ContainsKey(g.TeacherID)
                    ? teachers[g.TeacherID]
                    : null;

                if (teacher == null) return null;

                // Apply department filter
                if (!string.IsNullOrEmpty(department) && teacher.department != department)
                    return null;

                var peer = peerData
                    .FirstOrDefault(x => x.evaluateeID == g.TeacherID && x.courseCode == g.CourseCode)?.Avg;

                var student = studentData
                    .FirstOrDefault(x => x.teacherID == g.TeacherID && x.courseCode == g.CourseCode)?.Avg;

                var society = societyData
                    .FirstOrDefault(x => x.teacherID == g.TeacherID)?.Avg;

                var courseMgmt = courseMgmtData
                    .FirstOrDefault(x => x.teacherID == g.TeacherID)?.Avg;

                double weightedScore = 0;
                double usedWeight = 0;

                if (peer != null)
                {
                    weightedScore += (peer.Value / MAX_SCORE) * peerWeight;
                    usedWeight += peerWeight;
                }

                if (student != null)
                {
                    weightedScore += (student.Value / MAX_SCORE) * studentWeight;
                    usedWeight += studentWeight;
                }

                if (society != null)
                {
                    weightedScore += (society.Value / MAX_SCORE) * societyWeight;
                    usedWeight += societyWeight;
                }

                if (courseMgmt != null)
                {
                    weightedScore += (courseMgmt.Value / MAX_SCORE) * courseMgmtWeight;
                    usedWeight += courseMgmtWeight;
                }

                double percentage = usedWeight > 0
                    ? (weightedScore / usedWeight) * 100
                    : 0;

                return new
                {
                    TeacherID = g.TeacherID,
                    TeacherName = teacher.name,
                    Department = teacher.department,
                    CourseCode = g.CourseCode,
                    Percentage = Math.Round(percentage, 2)
                };
            })
            .Where(x => x != null)
            .ToList();

            return Ok(result);
        }

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
        public IHttpActionResult GetTeacherQuestionStatsFull(string teacherId,int sessionId,string evaluationType, string courseCode = null)
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

    }
}
