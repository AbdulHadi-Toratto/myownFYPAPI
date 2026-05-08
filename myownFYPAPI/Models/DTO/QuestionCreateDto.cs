using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class QuestionCreateDto
    {
        public string EvaluationType { get; set; }
        public List<QuestionItemDto> Questions { get; set; }
    }

    public class QuestionItemDto
    {
        public int Id { get; set; } // Create mein 0 hoga, Edit mein valid ID
        public string QuestionText { get; set; }
        public bool IsCritical { get; set; }
    }
}