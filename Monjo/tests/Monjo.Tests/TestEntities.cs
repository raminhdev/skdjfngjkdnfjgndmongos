using Monjo;

namespace Monjo.Tests
{
    /// <summary>Test entity exercising every supported column type + the full audit/soft-delete model.</summary>
    [MonjoTable("People")]
    [MonjoIndex("Name", unique: true)]
    [MonjoIndex("Age,State")]
    public class TestPerson : BaseEntity
    {
        public string Name { get; set; }
        public string? Nickname { get; set; }
        public int Age { get; set; }
        public decimal Balance { get; set; }
        public bool IsActive { get; set; }
        public PersonState State { get; set; }
        public Guid ReferenceId { get; set; }
        public DateTime? LastSeen { get; set; }
    }

    public enum PersonState { New, Active, Retired }

    /// <summary>Entity without soft-delete/audit fields (POCO).</summary>
    [MonjoTable("Counters")]
    public class TestCounter
    {
        public string Id { get; set; }
        public long Value { get; set; }
    }
}
