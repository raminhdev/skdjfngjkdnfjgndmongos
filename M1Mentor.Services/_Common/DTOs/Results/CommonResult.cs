namespace M1Mentor.Services._Common.DTOs.Results
{
    public class CommonResult
    {
        public string CreatedByInfo { get; set; }
        public DateTime CreatedMoment { get; set; } 

        public string ModifiedByInfo { get; set; }
        public DateTime? ModifiedMoment { get; set; }
    }
}
