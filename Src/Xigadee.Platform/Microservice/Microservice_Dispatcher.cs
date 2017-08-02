﻿#region Copyright
// Copyright Hitachi Consulting
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

#region using

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Caching;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endregion
namespace Xigadee
{
    //Dispatcher
    public partial class Microservice
    {
        #region Class -> TransmissionPayloadState
        /// <summary>
        /// This class holds the incoming payload state.
        /// </summary>
        public class TransmissionPayloadState
        {
            /// <summary>
            /// This is the default constructor.
            /// </summary>
            /// <param name="payload">The incoming payload.</param>
            /// <param name="maxTransitCount">The maximum transit count.</param>
            /// <param name="timerStart">The start time.</param>
            public TransmissionPayloadState(TransmissionPayload payload, int maxTransitCount, int timerStart)
            {
                Payload = payload;
                CurrentOptions = payload.Options;
                MaxTransitCount = maxTransitCount;
                TimerStart = timerStart;
            }
            /// <summary>
            /// This is the tickcount of when the incoming payload was first received.
            /// </summary>
            public int TimerStart { get; }

            /// <summary>
            /// This property determines if the incoming request was succesful.
            /// </summary>
            /// <returns></returns>
            public bool IsSuccess()
            {
                return TransmitSuccess || ExecuteSuccess;
            }
            /// <summary>
            /// This signals the payload as complete.
            /// </summary>
            public void Signal()
            {
                //Signal to the underlying listener that the message can be released.
                Payload.Signal(IsSuccess());
            }

            /// <summary>
            /// Set the transmission success. This is true by default. Do not change it.
            /// </summary>
            public bool TransmitSuccess { get; set; } = true;

            /// <summary>
            /// Set the execution success. This is true by default. Do not change it.
            /// </summary>
            public bool ExecuteSuccess { get; set; } = true;

            /// <summary>
            /// This is the incoming payload that needs to be processed.
            /// </summary>
            public TransmissionPayload Payload { get; }

            /// <summary>
            /// These are the response objects generated by the service.
            /// </summary>
            public List<TransmissionPayload> Responses { get; } = new List<TransmissionPayload>();

            /// <summary>
            /// This method validates the incoming payload and ensures that it is not null and that a
            /// message is available.
            /// </summary>
            public void IncomingValidate()
            {
                Payload.Cancel.ThrowIfCancellationRequested();

                //If there is no message then we cannot continue.
                if (Payload == null || Payload.Message == null)
                    throw new ArgumentNullException("Payload or Message not present");
            }
            /// <summary>
            /// This is the last exception generated during the execution
            /// </summary>
            public Exception Ex { get; set; }
            /// <summary>
            /// This is set to true if the request generated an exception during exception.
            /// </summary>
            public bool IsFaulted { get { return Ex != null; } }
            /// <summary>
            /// This provides custom routing instructions to the dispatcher.
            /// </summary>
            public ProcessOptions CurrentOptions { get; set; }
            /// <summary>
            /// This is the maximum number of attempts permitted.
            /// </summary>
            public int MaxTransitCount { get; }
            /// <summary>
            /// A boolean property that specifies whether the payload can only be processed externally.
            /// </summary>
            public bool ExternalOnly => (Payload.Options & ProcessOptions.RouteInternal) == 0;
            /// <summary>
            /// A boolean property that specifies if a payload can only be processed internally.
            /// </summary>
            public bool InternalOnly => (Payload.Options & ProcessOptions.RouteExternal) == 0;

            #region IncrementAndVerifyDispatcherTransitCount(TransmissionPayload request.Payload)
            /// <summary>
            /// This method checks that the incoming message has not exceeded the maximum number of hops.
            /// </summary>
            public void IncrementAndVerifyDispatcherTransitCount()
            {
                //Increase the Dispatcher transit count.
                Payload.Message.DispatcherTransitCount = Payload.Message.DispatcherTransitCount + 1;
                //Have we exceeded the transit count for the message.
                if (Payload.Message.DispatcherTransitCount > MaxTransitCount)
                    throw new TransitCountExceededException(Payload);
            }
            #endregion
        }
        #endregion

        #region -->Execute(TransmissionPayload requestPayload)
        /// <summary>
        /// This is the core method that messages are sent to to be routed and processed.
        /// You can override this task in your service to help debug the messages that are passing 
        /// though.
        /// </summary>
        /// <param name="requestPayload">The request payload.</param>
        protected virtual async Task Execute(TransmissionPayload requestPayload)
        {
            var request = new TransmissionPayloadState(requestPayload
                , Policy.Microservice.DispatcherTransitCountMax
                , StatisticsInternal.ActiveIncrement());

            try
            {
                Thread.CurrentPrincipal = request.Payload.SecurityPrincipal;

                mEventsWrapper.OnExecuteBegin(request);

                //Validate the imcoming request is correct and not cancelled.
                request.IncomingValidate();

                //Log the telemtry for the incoming message.
                mDataCollection.DispatcherPayloadIncoming(request.Payload);

                //Check that we have not exceeded the maximum transit count.
                request.IncrementAndVerifyDispatcherTransitCount();

                //Shortcut for external messages - send straight out
                if (request.ExternalOnly)
                    await TransmitPayload(request.Payload, request);
                else
                {
                    //Execute the message against the internal commands
                    await ExecuteCommands(request);

                    //OK, do we have any response from the commands to send on, both internal and external messages?
                    if (request.Responses.Count > 0)
                        await TransmitResponses(request);
                }
            }
            catch (DispatcherException dex)
            {
                mDataCollection.DispatcherPayloadException(dex.Payload, dex);

                mEventsWrapper.OnProcessRequestError(dex.Payload, dex);
            }
            catch (Exception ex)
            {
                mDataCollection.DispatcherPayloadException(request.Payload, ex);

                mEventsWrapper.OnProcessRequestError(request.Payload, ex);
            }
            finally
            {
                //Signal to the underlying listener that the message can be released.
                request.Signal();

                int delta = StatisticsInternal.ActiveDecrement(request.TimerStart);

                //Log the telemtry for the specific message channelId.
                mDataCollection.DispatcherPayloadComplete(request.Payload, delta, request.IsSuccess());

                if (!request.IsSuccess())
                    StatisticsInternal.ErrorIncrement();

                mEventsWrapper.OnExecuteComplete(request);

                //Set the thread principal to null before leaving.
                Thread.CurrentPrincipal = null;
            }
        }
        #endregion
        #region ExecuteCommands(TransmissionPayloadState request)
        /// <summary>
        /// This method executes the request against the command collection.
        /// </summary>
        /// <param name="request">The request state.</param>
        private async Task ExecuteCommands(TransmissionPayloadState request)
        {
            try
            {
                request.ExecuteSuccess = await mCommands.Execute(request.Payload, request.Responses);
            }
            catch (Exception ex)
            {
                request.Ex = ex;
            }

            //All good.
            if (request.ExecuteSuccess)
                return;

            //Switch the incoming message to the outgoing collection to be processed
            //by the sender as it has not been processed internally. This will happen if it 
            //is not marked as internal only and cannot be resolved locally.
            if (!request.InternalOnly)
            {
                //OK, we are going to send this to the senders, first make sure that this doesn't route back in.
                request.CurrentOptions = ProcessOptions.RouteExternal;
                //Switch the incoming message to the response payload so that they are picked up by the senders.
                request.Responses.Add(request.Payload);

                return;
            }

            ProcessUnhandledPayload(Policy.Microservice.DispatcherUnresolvedRequestMode
                , DispatcherRequestUnresolvedReason.MessageHandlerNotFound
                , request.Payload);
        }
        #endregion
        #region TransmitResponses(TransmissionPayloadState request)
        /// <summary>
        /// This method executes the request against the command collection.
        /// </summary>
        /// <param name="request">The request state.</param>
        private async Task TransmitResponses(TransmissionPayloadState request)
        {
            //Set the follow on security.
            request.Responses
                .Where((p) => p.SecurityPrincipal == null)
                .ForEach((p) => p.SecurityPrincipal = Thread.CurrentPrincipal as ClaimsPrincipal);

            //Get the payloads that can be processed internally.
            var internalPayload = request.Responses
                .Where((p) => ((request.CurrentOptions & ProcessOptions.RouteInternal) > 0) && mCommands.Resolve(p))
                .ToList();

            //OK, send the payloads off to the Task Manager for processing.
            internalPayload.ForEach((p) =>
            {
                //Mark internal only to stop any looping. 
                //We can do this as we have checked with the command handler that they will be processed
                p.Options = ProcessOptions.RouteInternal;
                mTaskManager.ExecuteOrEnqueue(p, "Dispatcher");
            });

            //Extract the payloads that have been processed internally so that we only have the external payloads left
            var externalPayload = request.Responses.Except(internalPayload).ToList();

            //Send the external payload to their specific destination in paralle;.
            await Task.WhenAll(externalPayload.Select(async (p) => await TransmitPayload(p, request)));
        }
        #endregion
        #region TransmitPayload(TransmissionPayload payload, TransmissionPayloadState request)
        /// <summary>
        /// This method transits a payload and captures and exceptions.
        /// </summary>
        /// <param name="payload">The payload to process.</param>
        /// <param name="request">The request state.</param>
        private async Task TransmitPayload(TransmissionPayload payload, TransmissionPayloadState request)
        {
            try
            {
                request.TransmitSuccess &= await Send(payload);
            }
            catch (Exception ex)
            {
                request.Ex = ex;
                request.TransmitSuccess = false;
            }
        } 
        #endregion
        #region Send(TransmissionPayload Payload)
        /// <summary>
        /// This method passes the payload to the communication container for transmission. This can be an incoming payload or
        /// a response payload generated through a request.
        /// </summary>
        /// <param name="Payload">The transmission payload.</param>
        /// <returns>Returns a boolean value if the transmission is successful.</returns>
        protected virtual async Task<bool> Send(TransmissionPayload Payload)
        {
            bool isSuccess = await mCommunication.Send(Payload);
            if (!isSuccess)
            {
                ProcessUnhandledPayload(Policy.Microservice.DispatcherInvalidChannelMode
                    , DispatcherRequestUnresolvedReason.ChannelOutgoingNotFound
                    , Payload);
            }
            return isSuccess;
        }
        #endregion

        /// <summary>
        /// This method processes an unhandled payload.
        /// </summary>
        /// <param name="policy">The action policy.</param>
        /// <param name="reason">The unhandled reason.</param>
        /// <param name="payload">The payload.</param>
        protected virtual void ProcessUnhandledPayload(DispatcherUnhandledAction policy
            , DispatcherRequestUnresolvedReason reason
            , TransmissionPayload payload)
        {
            //OK, we have an problem. We log this as an error and get out of here.
            mDataCollection.DispatcherPayloadUnresolved(payload, reason);

            var args = new DispatcherRequestUnresolvedEventArgs(payload, reason, policy);

            //Raise an event for the unresolved wrapper
            mEventsWrapper.OnProcessRequestUnresolved(args);

            //Process the policy. Note this can be changed in the response.
            switch (args.Policy)
            {
                case DispatcherUnhandledAction.Ignore:
                    break;
                case DispatcherUnhandledAction.AttemptResponseFailMessage:
                    if (!payload.CanRespond())
                        break;

                    var response = payload.ToResponse();
                    response.Message.StatusSet(501,args.Reason.ToString());
                    response.Message.ChannelPriority = -1;
                    Dispatch.Process(response);
                    //request.IsSuccess = true;
                    break;
                case DispatcherUnhandledAction.Exception:
                    //request.IsSuccess = true;
                    break;
            }
        }
    }
}
