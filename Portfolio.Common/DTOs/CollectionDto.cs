namespace Portfolio.Common.DTOs
{
    public class CollectionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Color { get; set; } = "#6c757d";
        public DateTime CreatedAt { get; set; }
        public DateTime? LastModified { get; set; }
    }

    public class CollectionCreateDto
    {
        public string Name { get; set; } = null!;
        public string? Color { get; set; }
    }

    public class CollectionUpdateDto
    {
        public string? Name { get; set; }
        public string? Color { get; set; }
    }
}