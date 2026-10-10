using VideoConverter.Core.Internal;
using VideoConverter.Core.Models;
using Xunit;

namespace VideoConverter.Core.Tests;

public class MediaInfoMapperTests
{
    private const string SampleFfprobeJson = """
    {
      "streams": [
        {
          "codec_type": "video",
          "codec_name": "h264",
          "width": 320,
          "height": 240,
          "avg_frame_rate": "15/1"
        },
        {
          "codec_type": "audio",
          "codec_name": "aac",
          "channels": 2,
          "sample_rate": "44100"
        }
      ],
      "format": {
        "filename": "C:\\media\\sample.mp4",
        "format_name": "mov,mp4,m4a,3gp,3g2,mj2",
        "duration": "5.024000",
        "size": "123456",
        "bit_rate": "196608"
      }
    }
    """;

    [Fact]
    public void FromJson_MapsAllFields()
    {
        var info = MediaInfoMapper.FromJson(SampleFfprobeJson, @"C:\media\sample.mp4");

        Assert.Equal("MP4", info.ContainerFormat);
        Assert.Equal(TimeSpan.FromSeconds(5.024), info.Duration);
        Assert.Equal(123456, info.SizeBytes);
        Assert.Equal(196608, info.BitRate);
        Assert.Equal("h264", info.VideoCodec);
        Assert.Equal(320, info.Width);
        Assert.Equal(240, info.Height);
        Assert.Equal(15d, info.FrameRate);
        Assert.Equal("aac", info.AudioCodec);
        Assert.Equal(2, info.AudioChannels);
        Assert.Equal(44100, info.SampleRate);
        Assert.True(info.HasVideo);
        Assert.True(info.HasAudio);
    }

    [Fact]
    public void FromJson_FractionalFrameRate_IsRoundedToThreeDecimals()
    {
        var info = MediaInfoMapper.FromJson(SampleFfprobeJson, "x");

        Assert.Equal(15d, info.FrameRate);
    }

    [Fact]
    public void ParseFrameRate_HandlesFraction()
    {
        Assert.Equal(29.97, MediaInfoMapper.ParseFrameRate("30000/1001"));
    }

    [Theory]
    [InlineData("0/0")]
    [InlineData("N/A")]
    [InlineData("")]
    [InlineData("30")]
    public void ParseFrameRate_InvalidValues_ReturnNull(string value)
    {
        Assert.Null(MediaInfoMapper.ParseFrameRate(value));
    }

    [Theory]
    [InlineData("N/A")]
    [InlineData("")]
    public void ParseDuration_InvalidValues_ReturnNull(string value)
    {
        Assert.Null(MediaInfoMapper.ParseDuration(value));
    }

    [Fact]
    public void ParseDuration_ParsesSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(12.5), MediaInfoMapper.ParseDuration("12.500000"));
    }

    [Fact]
    public void FromJson_AudioOnlyFile_HasNoVideoFields()
    {
        const string json = """
        {
          "streams": [
            { "codec_type": "audio", "codec_name": "mp3", "channels": 1, "sample_rate": "48000" }
          ],
          "format": { "format_name": "mp3", "duration": "3.5", "size": "100", "bit_rate": "128000" }
        }
        """;

        var info = MediaInfoMapper.FromJson(json, "a.mp3");

        Assert.False(info.HasVideo);
        Assert.True(info.HasAudio);
        Assert.Equal("MP3", info.ContainerFormat);
        Assert.Null(info.Width);
        Assert.Null(info.VideoCodec);
    }

    [Fact]
    public void FromJson_InvalidJson_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => MediaInfoMapper.FromJson("null", "a.mp4"));
    }
}
