// Service Modal Component
class ServiceModal {
    constructor() {
        this.createModal();
        this.setupEventListeners();
    }

    createModal() {
        const modal = document.createElement('div');
        modal.id = 'example-modal';
        modal.className = 'modal';
        modal.innerHTML = `
            <div class="modal-content">
                <button class="modal-close">&times;</button>
                <div class="modal-header">
                    <h2 id="modal-title"></h2>
                    <p id="modal-description"></p>
                    <div id="modal-tags" class="tag-list"></div>
                </div>
                <div class="modal-body">
                    <div class="info-grid">
                        <div class="info-item">
                            <h5>Complexity</h5>
                            <p id="modal-complexity" class="stars"></p>
                        </div>
                        <div class="info-item">
                            <h5>State Management</h5>
                            <p id="modal-state"></p>
                        </div>
                        <div class="info-item">
                            <h5>Unity Integration</h5>
                            <p id="modal-unity"></p>
                        </div>
                        <div class="info-item">
                            <h5>Context</h5>
                            <p id="modal-context"></p>
                        </div>
                    </div>

                    <div class="technical-decision">
                        <h4>Technical Implementation</h4>
                        <p id="modal-decision"></p>
                    </div>

                    <div class="code-section">
                        <h4>Interface</h4>
                        <div class="code-block">
                            <pre><code id="modal-interface"></code></pre>
                        </div>
                    </div>

                    <div class="code-section">
                        <h4>Implementation</h4>
                        <div class="code-block">
                            <pre><code id="modal-implementation"></code></pre>
                        </div>
                    </div>
                </div>
            </div>
        `;

        document.body.appendChild(modal);
    }

    setupEventListeners() {
        const modal = document.getElementById('example-modal');
        const closeButton = modal.querySelector('.modal-close');

        closeButton.addEventListener('click', () => this.close());

        // Close modal when clicking outside
        window.addEventListener('click', (event) => {
            if (event.target === modal) {
                this.close();
            }
        });

        // Close modal on escape key
        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape') {
                this.close();
            }
        });
    }

    show(example) {
        const modal = document.getElementById('example-modal');
        document.getElementById('modal-title').textContent = example.name;
        document.getElementById('modal-description').textContent = example.description;
        
        // Set complexity stars
        const complexityStars = {
            'Low': '★☆☆',
            'Medium': '★★☆',
            'High': '★★★'
        };
        document.getElementById('modal-complexity').textContent = complexityStars[example.complexity];
        
        document.getElementById('modal-state').textContent = example.has_state ? 'Stateful' : 'Stateless';
        document.getElementById('modal-unity').textContent = example.unity_integration === 'none' ? 'None' : 
            example.unity_integration === 'scene' ? 'Scene Integration' : 'Configuration';
        document.getElementById('modal-context').textContent = example.context === 'runtime' ? 'Runtime Only' :
            example.context === 'editor_only' ? 'Editor Only' : 'Runtime & Editor';
        
        document.getElementById('modal-tags').innerHTML = example.tags
            .map(tag => `<span class="advanced-tag">${tag}</span>`).join('');
        
        // Set technical decision
        const serviceType = this.determineServiceType(example);
        const lifetime = example.lifetime || 'singleton'; // Default to singleton if not specified
        const context = example.context === 'runtime' ? 'Runtime' :
            example.context === 'editor_only' ? 'Editor Only' : 'Runtime & Editor';
        
        document.getElementById('modal-decision').innerHTML = `
            <strong>Service Type:</strong> ${serviceType}<br>
            <strong>Lifetime:</strong> ${lifetime.charAt(0).toUpperCase() + lifetime.slice(1)}<br>
            <strong>Context:</strong> ${context}
        `;
        
        document.getElementById('modal-interface').textContent = example.code.interface;
        document.getElementById('modal-implementation').textContent = example.code.implementation;
        
        modal.style.display = 'block';
        document.body.style.overflow = 'hidden';
    }

    determineServiceType(example) {
        if (example.unity_integration === 'config' || (example.has_state && example.context === 'runtime_and_editor')) {
            return 'ScriptableObject Service';
        } else if (example.unity_integration === 'scene') {
            return 'MonoBehaviour Service';
        } else {
            return 'Regular C# Service';
        }
    }

    close() {
        const modal = document.getElementById('example-modal');
        modal.style.display = 'none';
        document.body.style.overflow = '';
    }
}

// Export the modal component
window.ServiceModal = ServiceModal; 