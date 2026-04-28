using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class SocietyEvaluationDTO
    {
        public string EvaluatorId { get; set; }
        public string EvaluateeId { get; set; }
        public int SocietyId { get; set; }
        public int QuestionId { get; set; }
        public int Score { get; set; }
        public int SessionId { get; set; }
        public string EvaluationType { get; set; }
    }
}