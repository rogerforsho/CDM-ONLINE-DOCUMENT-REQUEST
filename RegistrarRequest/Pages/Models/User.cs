namespace ProjectCapstone.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string StudentNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string ContactNumber { get; set; }
        public string Course { get; set; }
        public string YearLevel { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }


        public string FullName => $"{FirstName} {LastName}";
    }
}