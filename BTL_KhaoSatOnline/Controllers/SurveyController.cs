﻿// using đúng các namespace
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BTL_KhaoSatOnline.Models;
using BTL_KhaoSatOnline.Models.ViewModels;

namespace BTL_KhaoSatOnline.Controllers
{
    [Authorize]
    public class SurveyController : Controller
    {
        private readonly SurveyDbContext _context;

        public SurveyController(SurveyDbContext context)
        {
            _context = context;
        }

        // GET: Survey/Create
        public IActionResult Create()
        {
            var model = new SurveyCreateViewModel
            {
                Questions = new List<QuestionViewModel>
                {
                    new QuestionViewModel
                    {
                        Options = new List<OptionViewModel> { new OptionViewModel(), new OptionViewModel() }
                    }
                }
            };
            return View(model);
        }

        // POST: Survey/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SurveyCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var survey = new Survey
            {
                Title = model.Title,
                Description = model.Description,
                IsPublic = model.IsPublic,
                CreatorUserId = userId,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now
            };

            _context.Surveys.Add(survey);
            await _context.SaveChangesAsync(); // Để lấy được SurveyID

            var setting = new SurveySetting
            {
                SurveyId = survey.SurveyId,
                AllowMultipleResponses = model.AllowMultipleResponses,
                RequireLogin = model.RequireLogin,
                StartDate = model.StartDate,
                EndDate = model.EndDate
            };
            _context.SurveySettings.Add(setting);

            foreach (var qvm in model.Questions)
            {
                var question = new Question
                {
                    SurveyId = survey.SurveyId,
                    QuestionText = qvm.QuestionText,
                    QuestionType = qvm.QuestionType,
                    IsRequired = qvm.IsRequired
                };
                _context.Questions.Add(question);
                await _context.SaveChangesAsync(); // Để lấy QuestionID

                foreach (var opt in qvm.Options)
                {
                    var option = new Option
                    {
                        QuestionId = question.QuestionId,
                        OptionText = opt.OptionText
                    };
                    _context.Options.Add(option);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("MySurveys");
        }

        // GET: Survey/MySurveys
        public async Task<IActionResult> MySurveys()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var surveys = await _context.Surveys
                .Include(s => s.SurveySetting)
                .Where(s => s.CreatorUserId == userId)
                .ToListAsync();

            return View(surveys);
        }

        // GET: Survey/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var survey = await _context.Surveys
                .Include(s => s.SurveySetting)
                .Include(s => s.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(s => s.SurveyId == id);

            if (survey == null) return NotFound();

            var model = new SurveyCreateViewModel
            {
                Title = survey.Title,
                Description = survey.Description,
                IsPublic = survey.IsPublic,
                AllowMultipleResponses = survey.SurveySetting.AllowMultipleResponses,
                RequireLogin = survey.SurveySetting.RequireLogin,
                StartDate = survey.SurveySetting.StartDate,
                EndDate = survey.SurveySetting.EndDate,
                Questions = survey.Questions.Select(q => new QuestionViewModel
                {
                    QuestionText = q.QuestionText,
                    QuestionType = q.QuestionType,
                    IsRequired = q.IsRequired,
                    Options = q.Options.Select(o => new OptionViewModel
                    {
                        OptionText = o.OptionText
                    }).ToList()
                }).ToList()
            };

            ViewBag.SurveyId = survey.SurveyId;
            return View(model);
        }

        // POST: Survey/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SurveyCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var survey = await _context.Surveys
                .Include(s => s.SurveySetting)
                .Include(s => s.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(s => s.SurveyId == id);

            if (survey == null)
                return NotFound();

            survey.Title = model.Title;
            survey.Description = model.Description;
            survey.IsPublic = model.IsPublic;
            survey.LastModifiedDate = DateTime.Now;

            survey.SurveySetting.AllowMultipleResponses = model.AllowMultipleResponses;
            survey.SurveySetting.RequireLogin = model.RequireLogin;
            survey.SurveySetting.StartDate = model.StartDate;
            survey.SurveySetting.EndDate = model.EndDate;

            _context.Options.RemoveRange(survey.Questions.SelectMany(q => q.Options));
            _context.Questions.RemoveRange(survey.Questions);

            foreach (var qvm in model.Questions)
            {
                var question = new Question
                {
                    SurveyId = survey.SurveyId,
                    QuestionText = qvm.QuestionText,
                    QuestionType = qvm.QuestionType,
                    IsRequired = qvm.IsRequired
                };
                _context.Questions.Add(question);
                await _context.SaveChangesAsync();

                foreach (var opt in qvm.Options)
                {
                    var option = new Option
                    {
                        QuestionId = question.QuestionId,
                        OptionText = opt.OptionText
                    };
                    _context.Options.Add(option);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("MySurveys");
        }

        // GET: Survey/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var survey = await _context.Surveys
                .Include(s => s.Questions)
                .FirstOrDefaultAsync(m => m.SurveyId == id);

            if (survey == null) return NotFound();

            return View(survey);
        }

        // POST: Survey/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var survey = await _context.Surveys
                .Include(s => s.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(s => s.SurveyId == id);

            if (survey != null)
            {
                _context.Options.RemoveRange(survey.Questions.SelectMany(q => q.Options));
                _context.Questions.RemoveRange(survey.Questions);
                _context.SurveySettings.Remove(survey.SurveySetting);
                _context.Surveys.Remove(survey);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MySurveys");
        }

        // GET: Survey/Results/5
        public async Task<IActionResult> Results(int? id)
        {
            if (id == null) return NotFound();

            var survey = await _context.Surveys
                .Include(s => s.Questions)
                .ThenInclude(q => q.Answers)
                .ThenInclude(a => a.Option)
                .FirstOrDefaultAsync(s => s.SurveyId == id);

            if (survey == null) return NotFound();

            return View(survey);
        }
        [HttpGet]
        public async Task<IActionResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return View(new List<Survey>()); // hoặc trả về tất cả khảo sát nếu bạn muốn
            }

            var matchedSurveys = await _context.Surveys
                .Where(s => s.Title.Contains(query))
                .ToListAsync();

            return View(matchedSurveys);
        }
    }

}
