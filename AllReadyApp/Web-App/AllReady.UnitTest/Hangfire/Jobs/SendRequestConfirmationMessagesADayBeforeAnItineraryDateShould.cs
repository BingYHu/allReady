﻿using System;
using System.Collections.Generic;
using AllReady.Extensions;
using AllReady.Hangfire.Jobs;
using AllReady.Models;
using AllReady.Services;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Moq;
using Xunit;

namespace AllReady.UnitTest.Hangfire.Jobs
{
    public class SendRequestConfirmationMessagesADayBeforeAnItineraryDateShould : InMemoryContextTest
    {
        [Fact]
        public void NotSendRequestConfirmations_WhenRequestIdsDoNotMatchExistingRequests()
        {
            var request = new Request { RequestId = Guid.NewGuid() };

            var smsSender = new Mock<ISmsSender>();

            Context.Requests.Add(request);
            Context.SaveChanges();

            var sut = new SendRequestConfirmationMessagesADayBeforeAnItineraryDate(Context, Mock.Of<IBackgroundJobClient>(), smsSender.Object);
            sut.SendSms(new List<Guid> { Guid.NewGuid() }, It.IsAny<int>());

            smsSender.Verify(x => x.SendSmsAsync(It.IsAny<List<string>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void NotScheduleISendRequestConfirmationMessagesTheDayOfAnItineraryDate_WhenRequestIdsDoNotMatchExistingRequests()
        {
            var request = new Request { RequestId = Guid.NewGuid() };

            var backgroundJobClient = new Mock<IBackgroundJobClient>();

            Context.Requests.Add(request);
            Context.SaveChanges();

            var sut = new SendRequestConfirmationMessagesADayBeforeAnItineraryDate(Context, backgroundJobClient.Object, Mock.Of<ISmsSender>());
            sut.SendSms(new List<Guid> { Guid.NewGuid() }, It.IsAny<int>());

            backgroundJobClient.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<EnqueuedState>()), Times.Never);
        }

        [Fact]
        public void NotSendRequestConfirmations_WhenRequestIdsMatchExistingRequests_AndThoseRequestsDoNotHaveAStatusOfPendingConfirmation()
        {
            var request = new Request { RequestId = Guid.NewGuid(), Status = RequestStatus.Assigned };

            var smsSender = new Mock<ISmsSender>();

            Context.Requests.Add(request);
            Context.SaveChanges();

            var sut = new SendRequestConfirmationMessagesADayBeforeAnItineraryDate(Context, Mock.Of<IBackgroundJobClient>(), smsSender.Object);
            sut.SendSms(new List<Guid> { request.RequestId }, It.IsAny<int>());

            smsSender.Verify(x => x.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ScheduleISendRequestConfirmationMessagesTheDayOfAnItineraryDateWithCorrectParameters_WhenRequestIdsMatchExistingRequests_AndThoseRequestsHaveAStatusOfPendingConfirmation_AndTodayIsLessThanOneDayBeforeTheItineraryDate()
        {
            var dateTimeUtcNow = DateTime.UtcNow;

            var request = new Request { RequestId = Guid.NewGuid(), Status = RequestStatus.PendingConfirmation };
            var itinerary = new Itinerary { Id = 1, Date = dateTimeUtcNow.Date };

            var requestIds = new List<Guid> { request.RequestId };
            var backgroundJobClient = new Mock<IBackgroundJobClient>();

            Context.Requests.Add(request);
            Context.Itineraries.Add(itinerary);
            Context.SaveChanges();

            var sut = new SendRequestConfirmationMessagesADayBeforeAnItineraryDate(Context, backgroundJobClient.Object, Mock.Of<ISmsSender>()) { DateTimeUtcNow = () => dateTimeUtcNow.Date };
            sut.SendSms(requestIds, itinerary.Id);

            backgroundJobClient.Verify(x => x.Create(It.Is<Job>(job =>
                job.Type == typeof(ISendRequestConfirmationMessagesTheDayOfAnItineraryDate) &&
                job.Method.Name == nameof(ISendRequestConfirmationMessagesTheDayOfAnItineraryDate.SendSms) &&
                job.Args[0] == requestIds &&
                (int)job.Args[1] == itinerary.Id),
                It.Is<ScheduledState>(ss => ss.EnqueueAt.Date.AtNoon() == itinerary.Date.AtNoon())), Times.Once);
        }

        [Fact]
        public void NotSendRequestConfirmations_WhenRequestIdsMatchExistingRequests_AndThoseRequestsHaveAStatusOfPendingConfirmation_AndTodayIsLessThanOneDayBeforeTheItineraryDate()
        {
            var dateTimeUtcNow = DateTime.UtcNow;

            var request = new Request { RequestId = Guid.NewGuid(), Status = RequestStatus.PendingConfirmation };
            var itinerary = new Itinerary { Id = 1, Date = dateTimeUtcNow.Date };

            var backgroundJobClient = new Mock<IBackgroundJobClient>();
            var smsSender = new Mock<ISmsSender>();

            Context.Requests.Add(request);
            Context.Itineraries.Add(itinerary);
            Context.SaveChanges();

            var sut = new SendRequestConfirmationMessagesADayBeforeAnItineraryDate(Context, backgroundJobClient.Object, smsSender.Object) { DateTimeUtcNow = () => dateTimeUtcNow.Date };
            sut.SendSms(new List<Guid> { request.RequestId }, itinerary.Id);

            smsSender.Verify(x => x.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void SendRequestConfirmationsToTheCorrectPhoneNumbersWithTheCorrectMessage_WhenRequestIdsMatchExistingRequests_AndThoseRequestsArePendingConfirmation_AndTodayIsEqualToOrGreaterThanOneDaysBeforeTheItineraryDate()
        {
            var dateTimeUtcNow = DateTime.UtcNow;

            var requestorPhoneNumbers = new List<string> { "111-111-1111" };

            var request = new Request { RequestId = Guid.NewGuid(), Status = RequestStatus.PendingConfirmation, Phone = requestorPhoneNumbers[0] };
            var itinerary = new Itinerary { Id = 1, Date = dateTimeUtcNow.Date.AddDays(2) };

            var requestIds = new List<Guid> { request.RequestId };
            var smsSender = new Mock<ISmsSender>();
            var backgroundJobClient = new Mock<IBackgroundJobClient>();

            Context.Requests.Add(request);
            Context.Itineraries.Add(itinerary);
            Context.SaveChanges();

            var sut = new SendRequestConfirmationMessagesADayBeforeAnItineraryDate(Context, backgroundJobClient.Object, smsSender.Object) { DateTimeUtcNow = () => dateTimeUtcNow.Date };
            sut.SendSms(requestIds, itinerary.Id);

            var message = $@"Your request has been scheduled by allReady for {itinerary.Date.Date}. Please response with ""Y"" to confirm this request or ""N"" to cancel this request.";
            smsSender.Verify(x => x.SendSmsAsync(requestorPhoneNumbers, message), Times.Once);
        }
    }
}