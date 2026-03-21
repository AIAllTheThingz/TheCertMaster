using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TheCertMaster.Data;

#nullable disable

namespace TheCertMaster.Migrations
{
    [DbContext(typeof(QuizDbContext))]
    [Migration("20251230160000_AddQuizCategory")]
    public partial class AddQuizCategory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Quizzes",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Quizzes");
        }
    }
}
