using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamPrep.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScenariosPerForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScenariosPerForm",
                table: "exam_profiles",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScenariosPerForm",
                table: "exam_profiles");
        }
    }
}
