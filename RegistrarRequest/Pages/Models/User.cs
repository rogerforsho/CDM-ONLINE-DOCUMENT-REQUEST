using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ProjectCapstone.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("userId")]
        public int UserId { get; set; }

        [BsonElement("studentNumber")]
        public string StudentNumber { get; set; } = string.Empty;

        [BsonElement("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [BsonElement("lastName")]
        public string LastName { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [BsonElement("contactNumber")]
        public string? ContactNumber { get; set; }

        [BsonElement("course")]
        public string? Course { get; set; }

        [BsonElement("yearLevel")]
        public string? YearLevel { get; set; }

        [BsonElement("role")]
        public string Role { get; set; } = "Student";

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("lastLogin")]
        public DateTime? LastLogin { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
