namespace Monjo
{
    /// <summary>
    /// Common entity/document metadata for all providers (MongoDB, PostgreSQL, SQLite, future).
    /// Carries NO provider-specific attributes — SQL entities never see Mongo types and Mongo
    /// entities never see SQL types.
    /// </summary>
    /// <remarks>
    /// Property names are deliberately identical to the pre-existing <c>BaseDocument</c> so
    /// application code keeps compiling and behaving the same:
    /// <list type="bullet">
    /// <item><c>Id</c> — identifier (string or Guid; set <c>[MonjoId]</c> for other names/types).</item>
    /// <item><c>CreatedBy</c>/<c>CreatedByInfo</c>/<c>CreatedMoment</c> — filled at construction from <see cref="MonjoActorContext"/>.</item>
    /// <item><c>ModifiedBy</c>/<c>ModifiedByInfo</c>/<c>ModifiedMoment</c> — filled by Monjo on update.</item>
    /// <item><c>DeletedBy</c>/<c>DeletedByInfo</c>/<c>DeletedMoment</c>/<c>IsDeleted</c> — soft-delete fields, filled by Monjo on delete.</item>
    /// </list>
    /// The legacy Mongo <c>BaseDocument</c> (with its BSON attributes) is kept untouched in
    /// Monjo.MongoDB for source and data compatibility.
    /// </remarks>
    public class BaseEntity
    {
        public string Id { get; set; }

        public string CreatedBy { get; set; }
        public string CreatedByInfo { get; set; }
        public DateTime CreatedMoment { get; set; } = DateTime.UtcNow;

        public string ModifiedBy { get; set; }
        public string ModifiedByInfo { get; set; }
        public DateTime? ModifiedMoment { get; set; }

        public string DeletedBy { get; set; }
        public string DeletedByInfo { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedMoment { get; set; }

        public BaseEntity()
        {
            var actor = MonjoActorContext.Current;
            if (actor.HasIdentity)
            {
                CreatedBy = actor.PublicKey;
                CreatedByInfo = actor.DisplayInfo;
            }
            else
            {
                CreatedBy = "system";
                CreatedByInfo = "system : system";
            }
        }
    }
}
