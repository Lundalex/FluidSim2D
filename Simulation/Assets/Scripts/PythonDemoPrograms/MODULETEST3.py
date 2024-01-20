import matplotlib.pyplot as plt
import numpy as np
from matplotlib.patches import Rectangle

class TraverseLineSegmentDemo:
    def __init__(self):
        # Initialize start and end positions
        self.start_pos = np.array([2, 2])
        self.end_pos = np.array([10, 8])

        # Setup the plot
        self.fig, self.ax = plt.subplots()
        self.ax.set_xlim(0, 15)
        self.ax.set_ylim(0, 15)
        self.ax.set_aspect('equal', adjustable='box')
        self.ax.grid(True)

        # Draw initial line and traversed cells
        self.line, = self.ax.plot([self.start_pos[0], self.end_pos[0]], [self.start_pos[1], self.end_pos[1]], 'bo-')
        self.traversed_cells = []
        self.update_traversed_cells()

        # Connect event handlers
        self.cid = self.fig.canvas.mpl_connect('button_press_event', self.on_click)

    def update_traversed_cells(self):
        # Clear previous cells
        for rect in self.traversed_cells:
            rect.remove()
        self.traversed_cells.clear()

        # Traverse line segment and update cells
        for cell in self.traverse_line_segment(self.start_pos[0], self.start_pos[1], self.end_pos[0], self.end_pos[1]):
            rect = Rectangle((cell[0], cell[1]), 1, 1, color="yellow", alpha=0.3)
            self.ax.add_patch(rect)
            self.traversed_cells.append(rect)
        self.fig.canvas.draw_idle()

    @staticmethod
    def traverse_line_segment(startX, startY, endX, endY):
        traversed_chunks = []
        if startX > endX:
            startX, startY, endX, endY = endX, endY, startX, startY

        denominator = endX - startX
        if denominator == 0:
            return traversed_chunks
        k = (endY - startY) / denominator
        if k == 0:
            k = 0.0001
        SafetyFactor = int(min(1 + abs(k), 3))
        search_height = int(np.ceil(abs(k))) + SafetyFactor
        x_base = int(np.floor(startX)) - SafetyFactor
        y_base = int(np.floor(startY - k * SafetyFactor))

        for y1 in range(y_base - SafetyFactor, y_base + search_height + SafetyFactor):
            traversed_chunks.append((x_base, y1))

        dx = np.ceil(startX) - startX
        y_base += dx * k
        y_base_use = int(np.floor(y_base))
        x_base += 1

        for y in range(y_base_use - SafetyFactor, y_base_use + search_height + SafetyFactor):
            traversed_chunks.append((x_base, y))

        for x in range(x_base, int(np.floor(endX)) + 2):
            y_base += k
            y_base_use = int(np.floor(y_base))
            for y in range(y_base_use - SafetyFactor, y_base_use + search_height + SafetyFactor):
                traversed_chunks.append((x, y))

        return traversed_chunks

    def on_click(self, event):
        if event.inaxes != self.ax:
            return

        # Check if left or right mouse button was pressed
        if event.button == 1:
            # Left click - move start position
            self.start_pos = np.array([event.xdata, event.ydata])
        elif event.button == 3:
            # Right click - move end position
            self.end_pos = np.array([event.xdata, event.ydata])

        # Update the plot
        self.line.set_data([self.start_pos[0], self.end_pos[0]], [self.start_pos[1], self.end_pos[1]])
        self.update_traversed_cells()

# Create and show the demo
demo = TraverseLineSegmentDemo()
plt.show()
