using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ProjectCapstone.Models
{
    public class Department
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("departmentId")]
        public int DepartmentId { get; set; }

        [BsonElement("departmentCode")]
        public string DepartmentCode { get; set; } = string.Empty; // ITE, ICS, IEM

        [BsonElement("departmentName")]
        public string DepartmentName { get; set; } = string.Empty;

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
