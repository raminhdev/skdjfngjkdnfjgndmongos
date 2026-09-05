namespace M1Mentor.Services._FileMeta.DTOs.Settings
{
    public class FileSettings
    {
        public string EntityType1UploadPath { get; set; }
        public string EntityType2UploadPath { get; set; }
        public string EntityType3UploadPath { get; set; }
        public long MaxUploadSize { get; set; }
        public List<string> AllowedExtencions { get; set; }


        public int SafetyWindowHours { get; set; }
        public int QuarantinePeriodHours {  get; set; }
    }
}