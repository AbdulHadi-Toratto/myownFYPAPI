using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class ConfidentialEvaluationDto
    {
        public int EnrollmentId { get; set; }
        public string StudentId { get; set; }
        public List<CondentialAnswersDto> Answers { get; set; }
    }

    public class CondentialAnswersDto
    {
        public int questionId { get; set; }
        public int score { get; set; }
    }
}