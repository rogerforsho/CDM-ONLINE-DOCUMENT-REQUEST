using MongoDB.Driver;
using ProjectCapstone.Models;
using Microsoft.Extensions.Options;

namespace ProjectCapstone.Services
{
    public class MongoDBService
    {
        private readonly IMongoCollection<DocumentRequest> _documentRequests;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<SessionLog> _sessionLogs;
        private readonly IMongoCollection<DocumentType> _documentTypes;
        private readonly IMongoCollection<WorkflowHistory> _workflowHistory;
        private readonly IMongoCollection<Payment> _payments;
        private readonly IMongoDatabase _database;

        public MongoDBService(IOptions<MongoDBSettings> mongoDBSettings)
        {
            var mongoClient = new MongoClient(mongoDBSettings.Value.ConnectionString);
            _database = mongoClient.GetDatabase(mongoDBSettings.Value.DatabaseName);

            _documentRequests = _database.GetCollection<DocumentRequest>(mongoDBSettings.Value.DocumentRequestsCollection);
            _users = _database.GetCollection<User>(mongoDBSettings.Value.UsersCollection);
            _sessionLogs = _database.GetCollection<SessionLog>(mongoDBSettings.Value.SessionLogsCollection);
            _documentTypes = _database.GetCollection<DocumentType>(mongoDBSettings.Value.DocumentTypesCollection);
            _workflowHistory = _database.GetCollection<WorkflowHistory>(mongoDBSettings.Value.WorkflowHistoryCollection);
            _payments = _database.GetCollection<Payment>(mongoDBSettings.Value.PaymentsCollection);
        }

        // ================= DOCUMENT REQUEST METHODS =================
        public async Task<DocumentRequest> CreateRequestAsync(DocumentRequest request)
        {
            var lastRequest = await _documentRequests.Find(_ => true)
                .SortByDescending(r => r.RequestId)
                .FirstOrDefaultAsync();

            request.RequestId = (lastRequest?.RequestId ?? 0) + 1;

            await _documentRequests.InsertOneAsync(request);
            return request;
        }

        public async Task<List<DocumentRequest>> GetRequestsByUserIdAsync(int userId)
        {
            return await _documentRequests
                .Find(r => r.UserId == userId)
                .SortByDescending(r => r.RequestDate)
                .ToListAsync();
        }

        public async Task<int> GetCountByStatusAsync(int userId, string status)
        {
            return (int)await _documentRequests.CountDocumentsAsync(r => r.UserId == userId && r.Status == status);
        }

        public async Task<int> GetTotalCountAsync(int userId)
        {
            return (int)await _documentRequests.CountDocumentsAsync(r => r.UserId == userId);
        }

        public async Task<List<DocumentRequest>> GetHistoryAsync(int userId)
        {
            var filter = Builders<DocumentRequest>.Filter.And(
                Builders<DocumentRequest>.Filter.Eq(r => r.UserId, userId),
                Builders<DocumentRequest>.Filter.In(r => r.Status, new[] { "Completed", "Cancelled" })
            );

            return await _documentRequests.Find(filter)
                .SortByDescending(r => r.CompletedDate ?? r.RequestDate)
                .ToListAsync();
        }

        public async Task<DocumentRequest?> GetRequestByIdAsync(int requestId)
        {
            return await _documentRequests.Find(r => r.RequestId == requestId).FirstOrDefaultAsync();
        }

        public async Task<bool> UpdateRequestAsync(string id, DocumentRequest request)
        {
            var result = await _documentRequests.ReplaceOneAsync(r => r.Id == id, request);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteRequestAsync(string id)
        {
            var result = await _documentRequests.DeleteOneAsync(r => r.Id == id);
            return result.DeletedCount > 0;
        }

        // ================= USER METHODS =================
        public async Task<User?> GetUserByStudentNumberOrEmailAsync(string input)
        {
            var filter = Builders<User>.Filter.And(
                Builders<User>.Filter.Or(
                    Builders<User>.Filter.Eq(u => u.StudentNumber, input),
                    Builders<User>.Filter.Eq(u => u.Email, input)
                ),
                Builders<User>.Filter.Eq(u => u.IsActive, true)
            );

            return await _users.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
        }

        public async Task<User?> GetUserByStudentNumberAsync(string studentNumber)
        {
            return await _users.Find(u => u.StudentNumber == studentNumber).FirstOrDefaultAsync();
        }

        public async Task<User> CreateUserAsync(User user)
        {
            var lastUser = await _users.Find(_ => true)
                .SortByDescending(u => u.UserId)
                .FirstOrDefaultAsync();

            user.UserId = (lastUser?.UserId ?? 0) + 1;

            await _users.InsertOneAsync(user);
            return user;
        }

        public async Task<bool> UpdateUserLastLoginAsync(int userId)
        {
            var filter = Builders<User>.Filter.Eq(u => u.UserId, userId);
            var update = Builders<User>.Update.Set(u => u.LastLogin, DateTime.UtcNow);

            var result = await _users.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        // ================= SESSION LOG METHODS =================
        public async Task CreateSessionLogAsync(SessionLog log)
        {
            await _sessionLogs.InsertOneAsync(log);
        }

        // ================= DOCUMENT TYPE METHODS =================
        public async Task<List<DocumentType>> GetActiveDocumentTypesAsync()
        {
            return await _documentTypes.Find(dt => dt.IsActive)
                .SortBy(dt => dt.Category)
                .ThenBy(dt => dt.DocumentName)
                .ToListAsync();
        }

        public async Task<DocumentType?> GetDocumentTypeByIdAsync(int documentTypeId)
        {
            return await _documentTypes.Find(dt => dt.DocumentTypeId == documentTypeId && dt.IsActive)
                .FirstOrDefaultAsync();
        }

        // ================= WORKFLOW HISTORY =================
        public async Task CreateWorkflowHistoryAsync(WorkflowHistory history)
        {
            await _workflowHistory.InsertOneAsync(history);
        }

        public async Task<List<WorkflowHistory>> GetWorkflowHistoryByRequestIdAsync(int requestId)
        {
            return await _workflowHistory.Find(wh => wh.RequestId == requestId)
                .SortBy(wh => wh.ProcessedAt)
                .ToListAsync();
        }

        // ================= PAYMENT METHODS =================
        public async Task<Payment> CreatePaymentAsync(Payment payment)
        {
            var lastPayment = await _payments.Find(_ => true)
                .SortByDescending(p => p.PaymentId)
                .FirstOrDefaultAsync();

            payment.PaymentId = (lastPayment?.PaymentId ?? 0) + 1;

            await _payments.InsertOneAsync(payment);
            return payment;
        }

        public async Task<Payment?> GetPaymentByRequestIdAsync(int requestId)
        {
            return await _payments.Find(p => p.RequestId == requestId)
                .SortByDescending(p => p.PaymentDate)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> UpdatePaymentAsync(int paymentId, Payment payment)
        {
            var filter = Builders<Payment>.Filter.Eq(p => p.PaymentId, paymentId);
            var result = await _payments.ReplaceOneAsync(filter, payment);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateRequestStageAsync(int requestId, string currentStage, string paymentStatus)
        {
            var filter = Builders<DocumentRequest>.Filter.Eq(r => r.RequestId, requestId);

            var update = Builders<DocumentRequest>.Update
                .Set(r => r.CurrentStage, currentStage)
                .Set(r => r.PaymentStatus, paymentStatus);

            var result = await _documentRequests.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        // ================= ADMIN METHODS =================
        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _users.Find(u => u.UserId == userId).FirstOrDefaultAsync();
        }

        public async Task<List<DocumentRequest>> GetAllRequestsAsync()
        {
            return await _documentRequests.Find(_ => true).ToListAsync();
        }

        public async Task<List<RequestWithUser>> GetAllRequestsWithUsersAsync()
        {
            var requests = await _documentRequests.Find(_ => true).ToListAsync();
            var users = await _users.Find(_ => true).ToListAsync();

            return requests.Select(r =>
            {
                var user = users.FirstOrDefault(u => u.UserId == r.UserId);

                return new RequestWithUser
                {
                    RequestId = r.RequestId,
                    QueueNumber = r.QueueNumber,
                    DocumentType = r.DocumentType,
                    Purpose = r.Purpose,
                    Quantity = r.Quantity,
                    RequestDate = r.RequestDate,
                    Status = r.Status,
                    CompletedDate = r.CompletedDate,
                    StudentName = user != null ? $"{user.FirstName} {user.LastName}" : "",
                    StudentNumber = user?.StudentNumber ?? "",
                    StudentEmail = user?.Email ?? ""
                };
            }).ToList();
        }

        public async Task<bool> UpdateRequestStatusAsync(int requestId, string status, int processedBy, DateTime? completedDate = null)
        {
            var filter = Builders<DocumentRequest>.Filter.Eq(r => r.RequestId, requestId);

            var updateBuilder = Builders<DocumentRequest>.Update.Set(r => r.Status, status);

            if (completedDate.HasValue)
            {
                updateBuilder = updateBuilder.Set(r => r.CompletedDate, completedDate.Value);
            }

            var result = await _documentRequests.UpdateOneAsync(filter, updateBuilder);
            return result.ModifiedCount > 0;
        }
    }
}

// ================= HELPER CLASS =================
public class RequestWithUser
{
    public int RequestId { get; set; }
    public string QueueNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime RequestDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CompletedDate { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentNumber { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
}
