﻿using System;
using System.Linq;
using System.Threading.Tasks;
using AllReady.Areas.Admin.Models;
using AllReady.Models;
using MediatR;
using Microsoft.Data.Entity;

namespace AllReady.Areas.Admin.Features.Activities
{
    public class EditActivityCommandHandler : IAsyncRequestHandler<EditActivityCommand, int>
    {
        private AllReadyContext _context;

        public EditActivityCommandHandler(AllReadyContext context)
        {
            _context = context;

        }
        public async Task<int> Handle(EditActivityCommand message)
        {
            var activity = await GetActivity(message);

            if (activity == null)
            {
                activity = new Activity();
            }

            activity.Name = message.Activity.Name;
            activity.Description = message.Activity.Description;
            activity.ActivityType = message.Activity.ActivityType;

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(message.Activity.TimeZoneId);
            var startDateTimeOffset = timeZone.GetUtcOffset(message.Activity.StartDateTime);
            activity.StartDateTime = new DateTimeOffset(message.Activity.StartDateTime.Year, message.Activity.StartDateTime.Month, message.Activity.StartDateTime.Day, message.Activity.StartDateTime.Hour, message.Activity.StartDateTime.Minute, 0, startDateTimeOffset);

            var endDateTimeOffset = timeZone.GetUtcOffset(message.Activity.EndDateTime);
            activity.EndDateTime = new DateTimeOffset(message.Activity.EndDateTime.Year, message.Activity.EndDateTime.Month, message.Activity.EndDateTime.Day, message.Activity.EndDateTime.Hour, message.Activity.EndDateTime.Minute, 0, endDateTimeOffset);
            activity.CampaignId = message.Activity.CampaignId;
            
            activity.ImageUrl = message.Activity.ImageUrl;
            activity.NumberOfVolunteersRequired = message.Activity.NumberOfVolunteersRequired;

            if (activity.IsLimitVolunteers != message.Activity.IsLimitVolunteers || activity.IsAllowWaitList != message.Activity.IsAllowWaitList)
            {
                activity.IsAllowWaitList = message.Activity.IsAllowWaitList;
                activity.IsLimitVolunteers = message.Activity.IsLimitVolunteers;
                
                // cascade values to all tasks associated with this activity
                foreach (var task in _context.Tasks.Where(task => task.Activity.Id == activity.Id))
                {
                    task.IsLimitVolunteers = activity.IsLimitVolunteers;
                    task.IsAllowWaitList = activity.IsAllowWaitList;
                    _context.Update(task);
                }
            }

            if (activity.Id > 0)
            {
                var skillsToRemove = _context.ActivitySkills.Where(skill => skill.ActivityId == activity.Id && (message.Activity.RequiredSkills == null ||
                    !message.Activity.RequiredSkills.Any(ts1 => ts1.SkillId == skill.SkillId)));
                _context.ActivitySkills.RemoveRange(skillsToRemove);
            }

            if (message.Activity.RequiredSkills != null)
            {
                activity.RequiredSkills.AddRange(message.Activity.RequiredSkills.Where(mt => !activity.RequiredSkills.Any(ts => ts.SkillId == mt.SkillId)));
            }

            if (message.Activity.Location != null)
            {
                activity.Location = activity.Location.UpdateModel(message.Activity.Location);
                _context.Update(activity.Location);
            }

            _context.Update(activity);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            return activity.Id;
        }

        private async Task<Activity> GetActivity(EditActivityCommand message)
        {
            return await _context.Activities
                .Include(a => a.RequiredSkills)
                .SingleOrDefaultAsync(c => c.Id == message.Activity.Id)
                .ConfigureAwait(false);
        }
    }
}