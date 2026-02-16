using myownFYPAPI.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Routing;
using System.Web.Http;
using myownFYPAPI.Models.DTO;
namespace myownFYPAPI.Controllers.Director
{
    [RoutePrefix("api/Questionnaire")]

    public class QuestionnaireController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();

        [Route("Create")]
        public IHttpActionResult CreateQuestionnaire(QuestionCreateDto model)
        {
            if (model == null || model.Questions == null || model.Questions.Count == 0)
            {
                return BadRequest("Invalid data");
            }

            // 1️⃣ Create Questionnaire
            var questionnaire = new Questionare
            {
                type = model.EvaluationType,
                flag = "0" // DEFAULT — DO NOT CHANGE
            };

            db.Questionare.Add(questionnaire);
            db.SaveChanges(); // 🔥 ID generated here

            // 2️⃣ Insert Questions
            foreach (var q in model.Questions)
            {
                var question = new Questions
                {
                    QuestionareID = questionnaire.id,
                    QuestionText = q
                };

                db.Questions.Add(question);
            }

            db.SaveChanges();

            return Ok(new
            {
                message = "Questionnaire saved successfully",
                QuestionnaireId = questionnaire.id
            });
        }

        [HttpGet]
        [Route("GetAll")]
        public IHttpActionResult GetAll()
        {
            var data = db.Questionare
                .Select(q => new QestionnaireListDto
                {
                    Id = q.id,
                    Type = q.type,
                    Flag = q.flag,
                    QuestionCount = q.Questions.Count()
                })
                .ToList();

            return Ok(data);
        }

        [HttpPost]
        [Route("Toggle")]
        public IHttpActionResult ToggleQuestionnaire(ToggleQuestionnaireDto model)
        {
            var questionnaire = db.Questionare.Find(model.QuestionnaireId);

            if (questionnaire == null)
                return NotFound();

            if (model.TurnOn)
            {
                // ❌ Check if same type is already ON
                bool alreadyActive = db.Questionare.Any(q =>
                    q.type == questionnaire.type &&
                    q.flag == "1" &&
                    q.id != questionnaire.id
                );

                if (alreadyActive)
                {
                    return BadRequest("Another evaluation of this type is already active.");
                }

                questionnaire.flag = "1";
            }
            else
            {
                questionnaire.flag = "0";
            }

            db.SaveChanges();

            return Ok(new { message = "Status updated successfully" });
        }

        [HttpPost]
        [Route("SaveAllChanges")]
        public IHttpActionResult SaveAllChanges(SaveQuestionnaireChangesDto model)
        {
            if (model == null)
                return BadRequest("Invalid data");

            // 1️⃣ DELETE REMOVED QUESTIONS
            if (model.DeletedIds != null && model.DeletedIds.Count > 0)
            {
                var deleteQuestions = db.Questions
                    .Where(q => model.DeletedIds.Contains(q.QuestionID))
                    .ToList();

                foreach (var q in deleteQuestions)
                {
                    db.Questions.Remove(q);
                }

                //db.Questions.RemoveRange(deleteQuestions);
            }

            // 2️⃣ ADD & UPDATE QUESTIONS
            foreach (var q in model.Questions)
            {
                if (q.Id == 0)
                {
                    // ➕ NEW QUESTION
                    var newQuestion = new Questions
                    {
                        QuestionareID = model.QuestionnaireId,
                        QuestionText = q.QuestionText
                    };
                    db.Questions.Add(newQuestion);
                }
                else
                {
                    // ✏️ UPDATE EXISTING QUESTION
                    var existing = db.Questions.Find(q.Id);
                    if (existing != null)
                    {
                        existing.QuestionText = q.QuestionText;
                    }
                }
            }

            db.SaveChanges();

            return Ok(new
            {
                message = "Questionnaire updated successfully"
            });
        }



        [HttpGet]
        [Route("GetById/{id}")]
        public IHttpActionResult GetById(int id)
        {
            var questionnaire = db.Questionare
                .Where(q => q.id == id)
                .Select(q => new
                {
                    id = q.id,
                    title = q.type, // You don't have a separate title field, so using 'type' here
                    evaluationType = q.type,
                    questions = q.Questions.Select(qq => new
                    {
                        id = qq.QuestionID,
                        questionText = qq.QuestionText
                    })
                })
                .FirstOrDefault();

            if (questionnaire == null)
                return NotFound();

            return Ok(questionnaire);
        }


    }
}