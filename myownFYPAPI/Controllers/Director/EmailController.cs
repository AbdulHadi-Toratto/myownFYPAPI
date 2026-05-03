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
    [RoutePrefix("api/email")]
    public class EmailController : ApiController
    {
        fypapiv1Entities db = new fypapiv1Entities();

        [HttpGet]
        [Route("getall")]
        public IHttpActionResult GetAllEmails()
        {
            var emails = db.Email
                .Select(x => new
                {
                    x.id,
                    x.mail,
                    x.isActive
                })
                .OrderByDescending(x => x.id)
                .ToList();

            return Ok(emails);
        }

        [Route("getActive")]
        [HttpGet]
        public IHttpActionResult getActiveEmail()
        {
            var activeemail = db.Email.FirstOrDefault(x => x.isActive == true);

            if (activeemail == null)
                return NotFound();
            return Ok(activeemail);
        }

        [HttpPost]
        [Route("add")]
        public IHttpActionResult AddEmail(Email model)
        {
            if (model == null || string.IsNullOrEmpty(model.mail) || string.IsNullOrEmpty(model.password))
                return BadRequest("Email and App Password are required.");

            model.isActive = false;

            db.Email.Add(model);
            db.SaveChanges();

            return Ok(model);
        }

        [HttpDelete]
        [Route("delete/{id}")]
        public IHttpActionResult DeleteEmail(int id)
        {
            var email = db.Email.Find(id);

            if (email == null)
                return NotFound();

            db.Email.Remove(email);
            db.SaveChanges();

            return Ok("Deleted Successfully");


        }

        [HttpPut]
        [Route("activate/{id}")]
        public IHttpActionResult ActivateEmail(int id)
        {
            var emailToActivate = db.Email.Find(id);

            if (emailToActivate == null)
                return NotFound();

            // Check if another email is already active
            var alreadyActive = db.Email
                                  .FirstOrDefault(x => x.isActive == true);

            if (alreadyActive != null && alreadyActive.id != id)
            {
                return Content(HttpStatusCode.BadRequest,
                    "Another email is already active. Please deactivate it first.");
            }

            emailToActivate.isActive = true;
            db.SaveChanges();

            return Ok("Email Activated");
        }

        [HttpPut]
        [Route("deactivate/{id}")]
        public IHttpActionResult DeactivateEmail(int id)
        {
            var email = db.Email.Find(id);

            if (email == null)
                return NotFound();

            email.isActive = false;
            db.SaveChanges();

            return Ok("Email Deactivated");
        }

    }
}
