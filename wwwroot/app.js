const { createApp } = Vue;

createApp({
    data() {
        return {
            currentStep: 1,
            habit: '',
            questions: [],
            answers: {},
            plan: null,
            loadingQuestions: false,
            generatingPlan: false,
            generatingPDF: false,
            error: null,
            apiBaseUrl: window.location.origin // Uses the same domain as the frontend
        }
    },
    computed: {
        allQuestionsAnswered() {
            return this.questions.every(question => {
                const answer = this.answers[question.id];
                return answer && answer.toString().trim() !== '';
            });
        }
    },
    methods: {
        async fetchQuestions() {
            if (!this.habit.trim()) return;
            
            this.loadingQuestions = true;
            this.error = null;
            
            try {
                const response = await fetch(`${this.apiBaseUrl}/questions/${encodeURIComponent(this.habit.trim())}`);
                
                if (!response.ok) {
                    const errorData = await response.json().catch(() => ({ error: 'Unknown error occurred' }));
                    throw new Error(errorData.error || `HTTP ${response.status}: ${response.statusText}`);
                }
                
                const data = await response.json();
                this.questions = data.questions || [];
                this.answers = {};
                this.currentStep = 2;
                
            } catch (error) {
                console.error('Error fetching questions:', error);
                this.error = `Failed to get questions: ${error.message}`;
            } finally {
                this.loadingQuestions = false;
            }
        },
        
        async generatePlan() {
            if (!this.allQuestionsAnswered) return;
            
            this.generatingPlan = true;
            this.error = null;
            
            try {
                const requestBody = {
                    habit: this.habit.trim(),
                    answers: this.answers
                };
                
                const response = await fetch(`${this.apiBaseUrl}/plan`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify(requestBody)
                });
                
                if (!response.ok) {
                    const errorData = await response.json().catch(() => ({ error: 'Unknown error occurred' }));
                    throw new Error(errorData.error || `HTTP ${response.status}: ${response.statusText}`);
                }
                
                const data = await response.json();
                this.plan = data;
                this.currentStep = 3;
                
                // Scroll to top to show the plan
                window.scrollTo({ top: 0, behavior: 'smooth' });
                
            } catch (error) {
                console.error('Error generating plan:', error);
                this.error = `Failed to generate plan: ${error.message}`;
            } finally {
                this.generatingPlan = false;
            }
        },
        
        async downloadPDF() {
            if (!this.plan) return;
            
            this.generatingPDF = true;
            
            try {
                const { jsPDF } = window.jspdf;
                const doc = new jsPDF('p', 'mm', 'a4');
                
                // Set up page dimensions and margins
                const pageWidth = doc.internal.pageSize.getWidth();
                const pageHeight = doc.internal.pageSize.getHeight();
                const margin = 20;
                const contentWidth = pageWidth - (margin * 2);
                let currentY = margin;
                
                // Helper function to add text with word wrapping
                const addText = (text, x, y, options = {}) => {
                    const fontSize = options.fontSize || 12;
                    const fontStyle = options.fontStyle || 'normal';
                    const maxWidth = options.maxWidth || contentWidth;
                    
                    doc.setFontSize(fontSize);
                    doc.setFont('helvetica', fontStyle);
                    
                    const lines = doc.splitTextToSize(text, maxWidth);
                    doc.text(lines, x, y);
                    
                    return y + (lines.length * fontSize * 0.4);
                };
                
                // Helper function to check if we need a new page
                const checkNewPage = (requiredHeight) => {
                    if (currentY + requiredHeight > pageHeight - margin) {
                        doc.addPage();
                        currentY = margin;
                    }
                };
                
                // Header
                doc.setFillColor(102, 126, 234);
                doc.rect(0, 0, pageWidth, 40, 'F');
                
                doc.setTextColor(255, 255, 255);
                doc.setFontSize(20);
                doc.setFont('helvetica', 'bold');
                doc.text('🌱 Habit Builder Helper', margin, 25);
                
                doc.setFontSize(12);
                doc.setFont('helvetica', 'normal');
                doc.text('Your personalized 7-day habit building plan', margin, 35);
                
                currentY = 60;
                doc.setTextColor(0, 0, 0);
                
                // Plan Title
                doc.setFontSize(18);
                doc.setFont('helvetica', 'bold');
                currentY = addText(this.plan.planTitle, margin, currentY, { fontSize: 18, fontStyle: 'bold' });
                currentY += 10;
                
                // Habit
                doc.setFontSize(14);
                doc.setFont('helvetica', 'italic');
                currentY = addText(`Habit: ${this.habit}`, margin, currentY, { fontSize: 14, fontStyle: 'italic' });
                currentY += 15;
                
                // Generation date
                doc.setFontSize(10);
                doc.setFont('helvetica', 'normal');
                doc.setTextColor(128, 128, 128);
                currentY = addText(`Generated on: ${new Date().toLocaleDateString()}`, margin, currentY, { fontSize: 10 });
                currentY += 20;
                doc.setTextColor(0, 0, 0);
                
                // Daily plans
                this.plan.daily.forEach((day, index) => {
                    checkNewPage(80); // Check if we need space for the day content
                    
                    // Day header
                    doc.setFillColor(240, 240, 240);
                    doc.rect(margin, currentY - 5, contentWidth, 12, 'F');
                    
                    doc.setFontSize(14);
                    doc.setFont('helvetica', 'bold');
                    doc.setTextColor(102, 126, 234);
                    currentY = addText(`Day ${day.day}`, margin + 5, currentY + 5, { fontSize: 14, fontStyle: 'bold' });
                    currentY += 10;
                    doc.setTextColor(0, 0, 0);
                    
                    // Micro-Action
                    doc.setFontSize(12);
                    doc.setFont('helvetica', 'bold');
                    currentY = addText('Micro-Action:', margin, currentY, { fontSize: 12, fontStyle: 'bold' });
                    currentY += 2;
                    
                    doc.setFont('helvetica', 'normal');
                    currentY = addText(day.microAction, margin, currentY, { fontSize: 11 });
                    currentY += 8;
                    
                    // Reflection
                    doc.setFont('helvetica', 'bold');
                    currentY = addText('Reflection:', margin, currentY, { fontSize: 12, fontStyle: 'bold' });
                    currentY += 2;
                    
                    doc.setFont('helvetica', 'normal');
                    currentY = addText(day.reflection, margin, currentY, { fontSize: 11 });
                    currentY += 8;
                    
                    // Bible Verse
                    if (day.verseText) {
                        doc.setFont('helvetica', 'bold');
                        currentY = addText(`${day.verseReference}:`, margin, currentY, { fontSize: 12, fontStyle: 'bold' });
                        currentY += 2;
                        
                        doc.setFont('helvetica', 'italic');
                        doc.setTextColor(128, 128, 128);
                        currentY = addText(day.verseText, margin, currentY, { fontSize: 10, fontStyle: 'italic' });
                        currentY += 8;
                        doc.setTextColor(0, 0, 0);
                    }
                    
                    // Quote
                    if (day.quote) {
                        doc.setFont('helvetica', 'bold');
                        currentY = addText('Daily Quote:', margin, currentY, { fontSize: 12, fontStyle: 'bold' });
                        currentY += 2;
                        
                        doc.setFont('helvetica', 'italic');
                        currentY = addText(`"${day.quote}"`, margin, currentY, { fontSize: 10, fontStyle: 'italic' });
                        currentY += 2;
                        
                        if (day.quoteAuthor) {
                            doc.setFont('helvetica', 'normal');
                            doc.setTextColor(128, 128, 128);
                            currentY = addText(`- ${day.quoteAuthor}`, margin + 10, currentY, { fontSize: 9 });
                            doc.setTextColor(0, 0, 0);
                        }
                        currentY += 8;
                    }
                    
                    currentY += 5; // Extra space between days
                });
                
                // Footer
                const totalPages = doc.internal.getNumberOfPages();
                for (let i = 1; i <= totalPages; i++) {
                    doc.setPage(i);
                    doc.setFontSize(8);
                    doc.setFont('helvetica', 'normal');
                    doc.setTextColor(128, 128, 128);
                    doc.text(`Page ${i} of ${totalPages}`, pageWidth - margin - 20, pageHeight - 10);
                    doc.text('Generated by Habit Builder Helper', margin, pageHeight - 10);
                }
                
                // Generate filename
                const habitName = this.habit.replace(/[^a-zA-Z0-9]/g, '_');
                const timestamp = new Date().toISOString().split('T')[0];
                const filename = `habit_plan_${habitName}_${timestamp}.pdf`;
                
                // Save the PDF
                doc.save(filename);
                
            } catch (error) {
                console.error('Error generating PDF:', error);
                this.error = `Failed to generate PDF: ${error.message}`;
            } finally {
                this.generatingPDF = false;
            }
        },
        
        startOver() {
            this.currentStep = 1;
            this.habit = '';
            this.questions = [];
            this.answers = {};
            this.plan = null;
            this.error = null;
            this.loadingQuestions = false;
            this.generatingPlan = false;
            this.generatingPDF = false;
        },
        
        // Helper method to format text
        formatText(text) {
            if (!text) return '';
            return text.replace(/\n/g, '<br>');
        }
    },
    
    mounted() {
        // Optional: Add any initialization logic here
        console.log('Habit Builder Helper loaded successfully!');
    }
}).mount('#app');