using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace myownFYPAPI.Models.DTO
{
    public class SaveQuestionnaireChangesDto
    {
       
            public int QuestionnaireId { get; set; }
            public List<QuestionEditDto> Questions { get; set; }
            public List<int> DeletedIds { get; set; }
   

        
    }
    public class QuestionEditDto
    {
        public int Id { get; set; } // 0 for new question
        public string QuestionText { get; set; }
    }
}