﻿using System;

namespace Xigadee
{
    /// <summary>
    /// These extensions are used to attach a UDP based listener and sender to a channel
    /// </summary>
    public static partial class CorePipelineExtensionsCore
    {
        private static void UdpConfigChecks(UdpConfig udpDefault, (int priority, UdpConfig config)[] udpExtended)
        {
            if (udpDefault == null && udpExtended == null)
                throw new ArgumentNullException("udpDefault or udpExtended must be set");

            if (udpDefault == null && udpExtended.Length == 0)
                throw new ArgumentOutOfRangeException("udpDefault must be set or udpExtended must have at least one value");
        }

        /// <summary>
        /// Attaches the Udp sender to the outgoing channel.
        /// </summary>
        /// <typeparam name="C">The pipeline type.</typeparam>
        /// <param name="cpipe">The pipeline.</param>
        /// <param name="udpDefault">The UDP endpoint configuration.</param>
        /// <param name="shIdColl">Default service handler id collection.</param>
        /// <param name="serializer">This is an optional serializer that can be added with the specific mime type. Note:  the serializer mime type will be changed, so you should not share this serializer instance.</param>
        /// <param name="action">The optional action to be called when the sender is created.</param>
        /// <param name="maxUdpMessagePayloadSize">This is the max UDP message payload size. The default is 508 bytes. If you set this to null, the sender will not check the size before transmitting.</param>
        /// <param name="udpExtended">The extended UDP priority configuration.</param>
        /// <returns>Returns the pipeline.</returns>
        public static C AttachUdpSender<C>(this C cpipe
            , UdpConfig udpDefault = null
            , ServiceHandlerCollectionContext shIdColl = null
            , IServiceHandlerSerialization serializer = null
            , Action<ISender> action = null
            , int? maxUdpMessagePayloadSize = UdpConfig.PacketMaxSize
            , (int priority, UdpConfig config)[] udpExtended = null
            )
            where C : IPipelineChannelOutgoing<IPipeline>
        {
            UdpConfigChecks(udpDefault, udpExtended);

            if ((udpExtended?.Length??0) == 0)
                udpExtended = new[] {(1, udpDefault)};

            shIdColl = shIdColl ?? new ServiceHandlerCollectionContext();

            shIdColl.Serialization = (
                shIdColl.Serialization?.Id
                ?? serializer?.Id
                ?? $"udp_out/{cpipe.Channel.Id}"
                ).ToLowerInvariant();

            if (serializer != null)
                cpipe.Pipeline.AddPayloadSerializer(serializer);

            var sender = new UdpCommunicationAgent(udpExtended
                , CommunicationAgentCapabilities.Sender
                , shIdColl
                , maxUdpMessagePayloadSize: maxUdpMessagePayloadSize);

            cpipe.AttachSender(sender, action, true);

            return cpipe;
        }

        /// <summary>
        /// Attaches the Udp sender to the outgoing channel.
        /// </summary>
        /// <typeparam name="C">The pipeline type.</typeparam>
        /// <param name="cpipe">The pipeline.</param>
        /// <param name="udpDefault">The UDP endpoint configuration.</param>
        /// <param name="shIdColl">Default service handler id collection.</param>
        /// <param name="serialize">The serialize action.</param>
        /// <param name="canSerialize">The optional serialize check function.</param>
        /// <param name="action">The optional action to be called when the sender is created.</param>
        /// <param name="maxUdpMessagePayloadSize">This is the max UDP message payload size. The default is 508 bytes. If you set this to null, the sender will not check the size before transmitting.</param>
        /// <param name="udpExtended">The extended UDP priority configuration.</param>
        /// <returns>Returns the pipeline.</returns>
        public static C AttachUdpSender<C>(this C cpipe
            , UdpConfig udpDefault = null
            , ServiceHandlerCollectionContext shIdColl = null
            , Action<ServiceHandlerContext> serialize = null
            , Func<ServiceHandlerContext, bool> canSerialize = null
            , Action<ISender> action = null
            , int? maxUdpMessagePayloadSize = UdpConfig.PacketMaxSize
            , (int priority, UdpConfig config)[] udpExtended = null
            )
            where C : IPipelineChannelOutgoing<IPipeline>
        {
            UdpConfigChecks(udpDefault, udpExtended);

            IServiceHandlerSerialization serializer = null;

            shIdColl = shIdColl ?? new ServiceHandlerCollectionContext();

            shIdColl.Serialization = (
                shIdColl.Serialization?.Id?? $"udp_out/{cpipe.Channel.Id}"
                ).ToLowerInvariant();

            if (serialize != null)
            {
                serializer = CreateDynamicSerializer(shIdColl.Serialization.Id, serialize: serialize, canSerialize: canSerialize);
                shIdColl.Serialization = serializer.Id;
            }

            return cpipe.AttachUdpSender(
                  udpDefault
                , shIdColl
                , serializer
                , action
                , maxUdpMessagePayloadSize
                , udpExtended);
        }

    }
}
