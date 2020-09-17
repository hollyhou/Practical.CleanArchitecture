﻿using ClassifiedAds.Application.SmsMessages.DTOs;
using ClassifiedAds.CrossCuttingConcerns.OS;
using ClassifiedAds.Domain.Infrastructure.MessageBrokers;
using ClassifiedAds.Domain.Notification;
using ClassifiedAds.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace ClassifiedAds.Application.SmsMessages.Services
{
    public class SmsMessageService
    {
        private readonly ILogger _logger;
        private readonly ISmsMessageRepository _repository;
        private readonly IMessageSender<SmsMessageCreatedEvent> _smsMessageCreatedEventSender;
        private readonly ISmsNotification _smsNotification;
        private readonly IDateTimeProvider _dateTimeProvider;

        public SmsMessageService(ILogger<SmsMessageService> logger,
            ISmsMessageRepository repository,
            IMessageSender<SmsMessageCreatedEvent> smsMessageCreatedEventSender,
            ISmsNotification smsNotification,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _repository = repository;
            _smsMessageCreatedEventSender = smsMessageCreatedEventSender;
            _smsNotification = smsNotification;
            _dateTimeProvider = dateTimeProvider;
        }

        public int ResendSmsMessage()
        {
            var dateTime = _dateTimeProvider.OffsetNow.AddMinutes(-1);

            var messages = _repository.GetAll()
                .Where(x => x.SentDateTime == null && x.RetriedCount < 3)
                .Where(x => (x.RetriedCount == 0 && x.CreatedDateTime < dateTime) || (x.RetriedCount != 0 && x.UpdatedDateTime < dateTime))
                .ToList();

            if (messages.Any())
            {
                foreach (var sms in messages)
                {
                    _smsMessageCreatedEventSender.Send(new SmsMessageCreatedEvent { Id = sms.Id });
                    _repository.IncreaseRetry(sms.Id);
                }
            }
            else
            {
                _logger.LogInformation("No SMS to resend.");
            }

            return messages.Count;
        }

        public void SendSmsMessage(Guid id)
        {
            var smsMessage = _repository.GetAll().FirstOrDefault(x => x.Id == id);
            if (smsMessage != null && !smsMessage.SentDateTime.HasValue)
            {
                try
                {
                    _smsNotification.Send(smsMessage);
                    _repository.UpdateSent(smsMessage.Id);
                }
                catch (Exception ex)
                {
                    _repository.UpdateFailed(smsMessage.Id, Environment.NewLine + Environment.NewLine + ex.ToString());
                }
            }
        }
    }
}
