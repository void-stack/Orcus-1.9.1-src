﻿using System;
using OpusWrapper.Native;

namespace OpusWrapper
{
    /// <summary>
    ///     Opus codec wrapper.
    /// </summary>
    public class OpusEncoder : IDisposable
    {
        private bool _disposed;
        private IntPtr _encoder;

        private OpusEncoder(IntPtr encoder, int inputSamplingRate, int inputChannels, Application application)
        {
            _encoder = encoder;
            InputSamplingRate = inputSamplingRate;
            InputChannels = inputChannels;
            Application = application;
            MaxDataBytes = 4000;
        }

        /// <summary>
        ///     Gets the input sampling rate of the encoder.
        /// </summary>
        public int InputSamplingRate { get; private set; }

        /// <summary>
        ///     Gets the number of channels of the encoder.
        /// </summary>
        public int InputChannels { get; }

        /// <summary>
        ///     Gets the coding mode of the encoder.
        /// </summary>
        public Application Application { get; private set; }

        /// <summary>
        ///     Gets or sets the size of memory allocated for reading encoded data.
        ///     4000 is recommended.
        /// </summary>
        public int MaxDataBytes { get; set; }

        /// <summary>
        ///     Gets or sets the bitrate setting of the encoding.
        /// </summary>
        public int Bitrate
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException("OpusEncoder");
                int bitrate;
                var ret = OpusApi.opus_encoder_ctl(_encoder, Ctl.GetBitrateRequest, out bitrate);
                if (ret < 0)
                    throw new Exception("Encoder error - " + ((Errors) ret).ToString());
                return bitrate;
            }
            set
            {
                if (_disposed)
                    throw new ObjectDisposedException("OpusEncoder");
                var ret = OpusApi.opus_encoder_ctl(_encoder, Ctl.SetBitrateRequest, value);
                if (ret < 0)
                    throw new Exception("Encoder error - " + ((Errors) ret).ToString());
            }
        }

        /// <summary>
        ///     Gets or sets whether Forward Error Correction is enabled.
        /// </summary>
        public bool ForwardErrorCorrection
        {
            get
            {
                if (_encoder == IntPtr.Zero)
                    throw new ObjectDisposedException("OpusEncoder");

                int fec;
                int ret = OpusApi.opus_encoder_ctl(_encoder, Ctl.GetInbandFECRequest, out fec);
                if (ret < 0)
                    throw new Exception("Encoder error - " + ((Errors) ret).ToString());

                return fec > 0;
            }

            set
            {
                if (_encoder == IntPtr.Zero)
                    throw new ObjectDisposedException("OpusEncoder");

                var ret = OpusApi.opus_encoder_ctl(_encoder, Ctl.SetInbandFECRequest, value ? 1 : 0);
                if (ret < 0)
                    throw new Exception("Encoder error - " + ((Errors) ret).ToString());
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            GC.SuppressFinalize(this);

            if (_encoder != IntPtr.Zero)
            {
                OpusApi.opus_encoder_destroy(_encoder);
                _encoder = IntPtr.Zero;
            }

            _disposed = true;
        }

        /// <summary>
        ///     Creates a new Opus encoder.
        /// </summary>
        /// <param name="inputSamplingRate">
        ///     Sampling rate of the input signal (Hz). This must be one of 8000, 12000, 16000, 24000,
        ///     or 48000.
        /// </param>
        /// <param name="inputChannels">Number of channels (1 or 2) in input signal.</param>
        /// <param name="application">Coding mode.</param>
        /// <returns>A new <c>OpusEncoder</c></returns>
        public static OpusEncoder Create(int inputSamplingRate, int inputChannels, Application application)
        {
            if (inputSamplingRate != 8000 &&
                inputSamplingRate != 12000 &&
                inputSamplingRate != 16000 &&
                inputSamplingRate != 24000 &&
                inputSamplingRate != 48000)
                throw new ArgumentOutOfRangeException(nameof(inputSamplingRate));
            if (inputChannels != 1 && inputChannels != 2)
                throw new ArgumentOutOfRangeException(nameof(inputChannels));

            OpusLoader.LoadOpus();

            IntPtr error;
            IntPtr encoder = OpusApi.opus_encoder_create(inputSamplingRate, inputChannels, (int) application, out error);
            if ((Errors) error != Errors.OK)
            {
                throw new Exception("Exception occurred while creating encoder");
            }
            return new OpusEncoder(encoder, inputSamplingRate, inputChannels, application);
        }

        /// <summary>
        ///     Produces Opus encoded audio from PCM samples.
        /// </summary>
        /// <param name="inputPcmSamples">PCM samples to encode.</param>
        /// <param name="sampleLength">How many bytes to encode.</param>
        /// <param name="encodedLength">Set to length of encoded audio.</param>
        /// <returns>Opus encoded audio buffer.</returns>
        public unsafe byte[] Encode(byte[] inputPcmSamples, int sampleLength, out int encodedLength)
        {
            if (_disposed)
                throw new ObjectDisposedException("OpusEncoder");

            int frames = FrameCount(sampleLength);
            byte[] encoded = new byte[MaxDataBytes];
            int length;
            fixed (byte* benc = encoded)
            {
                var encodedPtr = new IntPtr(benc);
                length = OpusApi.opus_encode(_encoder, inputPcmSamples, frames, encodedPtr, sampleLength);
            }
            encodedLength = length;
            if (length < 0)
                throw new Exception("Encoding failed - " + (Errors) length);

            return encoded;
        }

        /// <summary>
        ///     Produces Opus encoded audio from PCM samples.
        /// </summary>
        /// <param name="inputPcmSamples">PCM samples to encode.</param>
        /// <param name="sampleLength">How many bytes to encode.</param>
        /// <param name="encodedLength">Set to length of encoded audio.</param>
        /// <returns>Opus encoded audio buffer.</returns>
        public unsafe byte[] Encode(byte* inputPcmSamples, int sampleLength, out int encodedLength)
        {
            if (_disposed)
                throw new ObjectDisposedException("OpusEncoder");

            int frames = FrameCount(sampleLength);
            byte[] encoded = new byte[MaxDataBytes];
            int length;
            fixed (byte* benc = encoded)
            {
                var encodedPtr = new IntPtr(benc);
                length = OpusApi.opus_encode(_encoder, inputPcmSamples, frames, encodedPtr, sampleLength);
            }
            encodedLength = length;
            if (length < 0)
                throw new Exception("Encoding failed - " + (Errors) length);

            return encoded;
        }

        /// <summary>
        ///     Determines the number of frames in the PCM samples.
        /// </summary>
        /// <param name="pcmSamplesLength"></param>
        /// <returns></returns>
        public int FrameCount(int pcmSamplesLength)
        {
            //  seems like bitrate should be required
            int bitrate = 16;
            int bytesPerSample = bitrate / 8 * InputChannels;
            return pcmSamplesLength / bytesPerSample;
        }

        /// <summary>
        ///     Helper method to determine how many bytes are required for encoding to work.
        /// </summary>
        /// <param name="frameCount">Target frame size.</param>
        /// <returns></returns>
        public int FrameByteCount(int frameCount)
        {
            int bitrate = 16;
            int bytesPerSample = bitrate / 8 * InputChannels;
            return frameCount * bytesPerSample;
        }

        ~OpusEncoder()
        {
            Dispose();
        }
    }
}