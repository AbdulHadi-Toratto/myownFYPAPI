using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class QuestionCreateDto
    {
        public string EvaluationType { get; set; }
        public List<string> Questions { get; set; }
    }
}