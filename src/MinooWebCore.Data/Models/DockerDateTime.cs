using System;
using System.Collections.Generic;
using System.Text;

namespace MinooWebCore.Data.Models
{
    public class DockerDateTime
    {
        public DateTime NowDateTime { get; set; }
        public DockerDateTime()
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
            NowDateTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;

        }
    }
    public static class DockerStaticDateTime
    {
        public static DateTime NowDateTime { get; set; }
        static DockerStaticDateTime()
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
            NowDateTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).UtcDateTime;
        }
        
    }
}
