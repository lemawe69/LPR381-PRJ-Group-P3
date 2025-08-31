using LinearProgrammingSolver.Algorithms;
using LinearProgrammingSolver.Models;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LinearProgrammingSolver
{
    public partial class MainForm : Form
    {
        private Button btnLoadProblem;
        private Button btnSolvePrimal;
        private Button btnSolveRevised;
        private Button btnSolveBranchAndBound;
        private Button btnSolveCuttingPlane;
        private Button btnSolveKnapsack;
        private Button btnExportResults;
        private TextBox txtProblemInput;
        private RichTextBox txtSolutionOutput;
        private Panel panelControls;
        private Panel panelContent;
        private OpenFileDialog openFileDialog;
        private SaveFileDialog saveFileDialog;
        private Label lblTitle;

        public MainForm()
        {
            InitializeComponents();
            SetupLayout();
            ApplyStyling();
        }

        private void InitializeComponents()
        {
            this.Text = "Linear Programming 381 Group P3";
            this.ClientSize = new Size(950, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);
            this.Font = new Font("Segoe UI", 10);

            lblTitle = new Label
            {
                Text = "Linear Programming 381 Group P3",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 60
            };

            panelContent = new Panel { Dock = DockStyle.Fill };
            panelControls = new Panel { Dock = DockStyle.Bottom, Height = 60 };

            txtProblemInput = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Left,
                Width = this.ClientSize.Width / 2 - 30
            };

            txtSolutionOutput = new RichTextBox
            {
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Both,
                Dock = DockStyle.Fill,
                WordWrap = false
            };

            btnLoadProblem = new Button { Text = "Load Problem", Width = 120, TabIndex = 0 };
            btnSolvePrimal = new Button { Text = "Primal Simplex", Width = 120, TabIndex = 1 };
            btnSolveRevised = new Button { Text = "Revised Simplex", Width = 130, TabIndex = 2 };
            btnSolveBranchAndBound = new Button { Text = "Branch and Bound Algorithm", Width = 250, TabIndex = 3 };
            btnSolveCuttingPlane = new Button { Text = "Cutting Plane Algorithm", Width = 190, TabIndex = 4 };
            btnSolveKnapsack = new Button { Text = "Branch and Bound Knapsack algorithm", Width = 300, TabIndex = 5 };
            btnExportResults = new Button { Text = "Export Results", Width = 120, TabIndex = 6 };

            openFileDialog = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Select LP Problem File"
            };

            saveFileDialog = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|CSV Files (*.csv)|*.csv",
                Title = "Save Solution Results"
            };

            btnLoadProblem.Click += BtnLoadProblem_Click;
            btnSolvePrimal.Click += BtnSolvePrimal_Click;
            btnSolveRevised.Click += BtnSolveRevised_Click;
            btnSolveBranchAndBound.Click += BtnSolveBranchAndBound_Click;
            btnSolveCuttingPlane.Click += BtnSolveCuttingPlane_Click;
            btnSolveKnapsack.Click += BtnSolveKnapsack_Click;
            btnExportResults.Click += BtnExportResults_Click;
        }

        private void SetupLayout()
        {
            this.Controls.Add(panelContent);
            this.Controls.Add(lblTitle);
            this.Controls.Add(panelControls);

            panelContent.Controls.Add(txtSolutionOutput);
            panelContent.Controls.Add(txtProblemInput);

            panelControls.Controls.Add(btnLoadProblem);
            panelControls.Controls.Add(btnSolvePrimal);
            panelControls.Controls.Add(btnSolveRevised);
            panelControls.Controls.Add(btnSolveBranchAndBound);
            panelControls.Controls.Add(btnSolveCuttingPlane);
            panelControls.Controls.Add(btnSolveKnapsack);
            panelControls.Controls.Add(btnExportResults);

            //int padding = (panelControls.Width - (4 * 120 + 3 * 10)) / 2;
            int padding = 80;
            btnLoadProblem.Left = padding;
            btnSolvePrimal.Left = btnLoadProblem.Right + 10;
            btnSolveRevised.Left = btnSolvePrimal.Right + 10;
            btnSolveBranchAndBound.Left = btnSolveRevised.Right + 10;
            btnSolveCuttingPlane.Left = btnSolveBranchAndBound.Right + 10;
            btnSolveKnapsack.Left = btnSolveCuttingPlane.Right + 10;
            btnExportResults.Left = btnSolveKnapsack.Right + 10;

            int top = panelControls.Height / 2 - btnLoadProblem.Height / 2;
            btnLoadProblem.Top = top;
            btnSolvePrimal.Top = top;
            btnSolveRevised.Top = top;
            btnSolveBranchAndBound.Top = top;
            btnSolveCuttingPlane.Top = top;
            btnSolveKnapsack.Top = top;
            btnExportResults.Top = top;
        }

        private void ApplyStyling()
        {
            Color primaryColor = Color.FromArgb(0, 120, 215);
            Color secondaryColor = Color.FromArgb(240, 240, 240);
            Color accentColor = Color.FromArgb(30, 57, 91);
            Color buttonHoverColor = Color.FromArgb(0, 90, 158);

            this.BackColor = secondaryColor;
            this.ForeColor = Color.FromArgb(64, 64, 64);

            lblTitle.BackColor = accentColor;
            lblTitle.ForeColor = Color.White;

            panelContent.BackColor = Color.White;
            panelControls.BackColor = Color.White;
            panelControls.Padding = new Padding(0, 10, 0, 0);

            txtProblemInput.BorderStyle = BorderStyle.FixedSingle;
            txtProblemInput.BackColor = Color.White;
            txtProblemInput.ForeColor = Color.FromArgb(64, 64, 64);
            txtProblemInput.Margin = new Padding(20, 10, 10, 10);

            txtSolutionOutput.BorderStyle = BorderStyle.FixedSingle;
            txtSolutionOutput.BackColor = Color.White;
            txtSolutionOutput.ForeColor = Color.FromArgb(64, 64, 64);
            txtSolutionOutput.Margin = new Padding(10, 10, 20, 10);

            foreach (Button btn in panelControls.Controls)
            {
                btn.BackColor = primaryColor;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
                btn.ForeColor = Color.White;
                btn.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                btn.Height = 36;
                btn.Cursor = Cursors.Hand;
                btn.FlatAppearance.MouseOverBackColor = buttonHoverColor;
                btn.FlatAppearance.MouseDownBackColor = accentColor;
            }
        }

        private void BtnLoadProblem_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() != DialogResult.OK) return;

            try
            {
                txtProblemInput.Text = File.ReadAllText(openFileDialog.FileName);
                txtSolutionOutput.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSolvePrimal_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtProblemInput.Text))
            {
                MessageBox.Show("Please load a problem first.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                txtSolutionOutput.Text = "Running Primal Simplex algorithm...\n\n";

                var program = LinearProgram.Parse(txtProblemInput.Text);
                var solver = new PrimalSimplex();
                var solution = solver.Solve(program);

                txtSolutionOutput.Text += solution.ToString();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error solving problem: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSolveRevised_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtProblemInput.Text))
            {
                MessageBox.Show("Please load a problem first.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                txtSolutionOutput.Text = "Running Revised Simplex algorithm...\n\n";

                var program = LinearProgram.Parse(txtProblemInput.Text);
                var solver = new RevisedPrimalSimplex();
                var solution = solver.Solve(program);

                txtSolutionOutput.Text += solution.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error solving problem: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        /////////////////////////////////////////////////////////////////////////////////////////////////
        // PART 3
        /////////////////////////////////////////////////////////////////////////////////////////////////
        private async void BtnSolveBranchAndBound_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtProblemInput.Text))
            {
                MessageBox.Show("Please load a problem first.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                btnSolveBranchAndBound.Enabled = false;
                txtSolutionOutput.Text = "Running Branch and Bound algorithm...\n\n";

                var program = LinearProgram.Parse(txtProblemInput.Text);
                var solver = new BranchAndBound();
                var solution = await Task.Run(() => solver.Solve(program));

                txtSolutionOutput.Text += solution.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error solving problem: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSolveBranchAndBound.Enabled = true;
            }
        }
        /////////////////////////////////////////////////////////////////////////////////////////////////
        private async void BtnSolveCuttingPlane_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtProblemInput.Text))
            {
                MessageBox.Show("Please load a problem first.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                btnSolveCuttingPlane.Enabled = false;
                txtSolutionOutput.Text = "Running Cutting Plane algorithm...\n\n";

                var program = LinearProgram.Parse(txtProblemInput.Text);
                var solver = new LinearProgrammingSolver.Algorithms.CuttingPlane();
                var solution = await Task.Run(() => solver.Solve(program));

                txtSolutionOutput.Text += solution.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error solving problem: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSolveCuttingPlane.Enabled = true;
            }
        }
        /////////////////////////////////////////////////////////////////////////////////////////////////
        private void BtnSolveKnapsack_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtProblemInput.Text))
            {
                MessageBox.Show("Please load a problem first.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                btnSolveCuttingPlane.Enabled = false;
                txtSolutionOutput.Text = "Running Branch and Bound Knapsack algorithm...\n\n";

                var program = LinearProgram.Parse(txtProblemInput.Text);
                var solver = new LinearProgrammingSolver.Algorithms.Knapsack();
                var solution = solver.Solve(program);

                txtSolutionOutput.Text += solution.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error solving problem: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSolveCuttingPlane.Enabled = true;
            }
        }
        /////////////////////////////////////////////////////////////////////////////////////////////////
        private void BtnExportResults_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSolutionOutput.Text))
            {
                MessageBox.Show("No results to export.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (saveFileDialog.ShowDialog() != DialogResult.OK) return;

            try
            {
                File.WriteAllText(saveFileDialog.FileName, txtSolutionOutput.Text);
                MessageBox.Show("Results exported successfully.", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting results: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        [STAThread]
        public static void Main1()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
