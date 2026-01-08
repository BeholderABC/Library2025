// 通知系统
function showNotification(type, title, message, duration = 3000) {
  const container = document.getElementById('notificationContainer');
  
  // 创建通知元素
  const notification = document.createElement('div');
  notification.className = `notification ${type}`;
  
  // 根据类型选择图标
  let icon = '';
  switch(type) {
    case 'success':
      icon = '✓';
      break;
    case 'error':
      icon = '✕';
      break;
    case 'warning':
      icon = '⚠';
      break;
    case 'info':
      icon = 'ℹ';
      break;
    default:
      icon = 'ℹ';
  }
  
  notification.innerHTML = `
    <div class="notification-icon">${icon}</div>
    <div class="notification-content">
      <div class="notification-title">${title}</div>
      ${message ? `<div class="notification-message">${message}</div>` : ''}
    </div>
    <button class="notification-close" onclick="this.parentElement.remove()">×</button>
  `;
  
  // 添加到容器
  container.appendChild(notification);
  
  // 触发显示动画
  setTimeout(() => {
    notification.classList.add('show');
  }, 100);
  
  // 自动移除
  setTimeout(() => {
    if (notification.parentElement) {
      notification.classList.remove('show');
      setTimeout(() => {
        if (notification.parentElement) {
          notification.remove();
        }
      }, 400);
    }
  }, duration);
  
  return notification;
}

// 快捷方法
function showSuccess(title, message) {
  return showNotification('success', title, message);
}

function showError(title, message) {
  return showNotification('error', title, message);
}

function showWarning(title, message) {
  return showNotification('warning', title, message);
}

function showSuccessMessage(message) {
  showSuccess('操作成功', message);
}

class Pagination {
  /**
   * 构造函数
   * @param {Object} options - 配置选项
   * @param {string} options.containerId - 分页容器的ID
   * @param {string} options.itemsSelector - 需要分页的元素选择器
   * @param {number} options.pageSize - 每页显示的项目数量
   * @param {number} options.totalItems - 总项目数量
   * @param {number} options.pagedContainer - 被分页容器ID
   * @param {number} [options.initialPage=1] - 初始页码
   * @param {string} [options.pageParam='page'] - URL中页码参数名
   * @param {Function} [options.onPageChange] - 页码改变时的回调函数
   * @param {Function} [options.onInitialize] - 初始化完成后的回调函数
   */
  constructor(options) {
    this.containerId = options.containerId;
    this.itemsSelector = options.itemsSelector;
    this.pageSize = options.pageSize;
    this.totalItems = options.totalItems;
    this.pagedContainer = options.pagedContainer;
    this.pageParam = options.pageParam || 'page';
    this.onPageChange = options.onPageChange || null;
    this.onInitialize = options.onInitialize || null;
    
    // 计算总页数
    this.totalPages = Math.max(1, Math.ceil(this.totalItems / this.pageSize));
    
    // 设置初始页码
    this.currentPage = this.getInitialPage(options.initialPage);
    
    // 获取DOM元素
    this.container = document.getElementById(this.containerId);
    this.items = document.querySelectorAll(this.itemsSelector);
    
    if (!this.container) {
      console.error(`分页容器 #${this.containerId} 未找到`);
      return;
    }
    
    // 初始化
    this.init();
  }
  
  /**
   * 获取初始页码
   * @param {number} defaultPage - 默认页码
   * @returns {number} - 有效的初始页码
   */
  getInitialPage(defaultPage = 1) {
    // 优先从URL参数获取
    const urlPage = this.getUrlParameter(this.pageParam);
    let page = defaultPage;
    
    if (urlPage) {
      const parsedPage = parseInt(urlPage, 10);
      if (!isNaN(parsedPage) && parsedPage > 0) {
        page = parsedPage;
      }
    }
    
    // 确保页码在有效范围内
    return Math.min(Math.max(1, page), this.totalPages);
  }
  
  /**
   * 获取URL参数
   * @param {string} name - 参数名
   * @returns {string|null} - 参数值
   */
  getUrlParameter(name) {
    const urlParams = new URLSearchParams(window.location.search);
    return urlParams.get(name);
  }
  
  /**
   * 更新URL参数
   * @param {string} key - 参数键
   * @param {string|number} value - 参数值
   */
  updateUrlParameter(key, value) {
    const url = new URL(window.location);
    url.searchParams.set(key, value);
    window.history.pushState({}, '', url);
  }
  
  /**
   * 显示指定页码的项目
   * @param {number} page - 页码
   */
  showItemsForPage(page) {
    const start = (page - 1) * this.pageSize;
    const end = start + this.pageSize;
    
    this.items.forEach((item, index) => {
      if (index >= start && index < end) {
        item.classList.add('visible');
      } else {
        item.classList.remove('visible');
      }
    });
  }
  
  /**
   * 生成分页按钮HTML
   * @returns {string} - 分页按钮的HTML字符串
   */
  generatePaginationHTML() {
    let html = '';
    
    // 上一页按钮
    const prevDisabled = this.currentPage === 1 ? 'disabled' : '';
    html += `<button class="page-btn ${prevDisabled}" data-page="${Math.max(1, this.currentPage - 1)}">‹</button>`;
    
    // 页码按钮逻辑
    if (this.totalPages <= 7) {
      // 总页数不超过7页，显示所有页码
      for (let i = 1; i <= this.totalPages; i++) {
        const active = i === this.currentPage ? 'active' : '';
        html += `<button class="page-btn ${active}" data-page="${i}">${i}</button>`;
      }
    } else {
      // 总页数超过7页的复杂逻辑
      html += `<button class="page-btn ${this.currentPage === 1 ? 'active' : ''}" data-page="1">1</button>`;
      
      if (this.currentPage > 3) {
        html += '<span class="page-btn page-ellipsis">...</span>';
      }
      
      const startPage = Math.max(2, this.currentPage - 1);
      const endPage = Math.min(this.totalPages - 1, this.currentPage + 1);
      
      for (let i = startPage; i <= endPage; i++) {
        const active = i === this.currentPage ? 'active' : '';
        html += `<button class="page-btn ${active}" data-page="${i}">${i}</button>`;
      }
      
      if (this.currentPage < this.totalPages - 2) {
        html += '<span class="page-btn page-ellipsis">...</span>';
      }
      
      if (this.totalPages > 1) {
        const active = this.currentPage === this.totalPages ? 'active' : '';
        html += `<button class="page-btn ${active}" data-page="${this.totalPages}">${this.totalPages}</button>`;
      }
    }
    
    // 下一页按钮
    const nextDisabled = this.currentPage === this.totalPages ? 'disabled' : '';
    html += `<button class="page-btn ${nextDisabled}" data-page="${Math.min(this.totalPages, this.currentPage + 1)}">›</button>`;
    
    // 页码信息
    html += `<div class="page-info">第 ${this.currentPage} 页，共 ${this.totalPages} 页，总计 ${this.totalItems} 个项目</div>`;
    
    return html;
  }
  
  /**
   * 渲染分页组件
   */
  render() {
    if (this.totalItems === 0) {
      this.container.innerHTML = '';
      return;
    }
    
    this.container.innerHTML = this.generatePaginationHTML();
    this.bindEvents();
  }
  
  /**
   * 绑定分页按钮事件
   */
  bindEvents() {
    // 为所有分页按钮绑定点击事件（使用事件委托）
    this.container.addEventListener('click', (e) => {
      if (e.target.classList.contains('page-btn') && !e.target.classList.contains('disabled') && !e.target.classList.contains('page-ellipsis')) {
        const page = parseInt(e.target.dataset.page, 10);
        if (!isNaN(page)) {
          this.goToPage(page);
        }
      }
    });
  }
  
  /**
   * 跳转到指定页码
   * @param {number} page - 目标页码
   */
  goToPage(page) {
    // 验证页码有效性
    if (page < 1 || page > this.totalPages || page === this.currentPage) {
      return;
    }
    
    // 更新当前页码
    this.currentPage = page;
    
    // 更新URL参数
    this.updateUrlParameter(this.pageParam, page);
    
    // 显示对应页面的项目
    this.showItemsForPage(page);
    
    // 重新渲染分页组件
    this.render();
    
    // 执行页码改变回调
    if (this.onPageChange && typeof this.onPageChange === 'function') {
      this.onPageChange(page, this);
    }
    
    // 平滑滚动到容器顶部
    this.scrollToTop();
  }
  
  /**
   * 滚动到顶部
   */
  scrollToTop() {
    // 查找包含项目的容器并滚动到顶部
    // const itemsContainer = document.querySelector(this.itemsSelector)?.closest(this.pagedContainerClass) || 
    //                       document.querySelector(`#${this.pagedContainerId}`);
    const itemsContainer = document.querySelector(`#${this.pagedContainer}`);
    
    if (itemsContainer) {
      itemsContainer.scrollIntoView({ 
        behavior: 'smooth', 
        block: 'start' 
      });
    }
  }
  
  /**
   * 初始化分页组件
   */
  init() {
    if (this.totalItems > 0) {
      this.showItemsForPage(this.currentPage);
      this.render();
    }
    
    // 执行初始化完成回调
    if (this.onInitialize && typeof this.onInitialize === 'function') {
      this.onInitialize(this);
    }
  }
  
  /**
   * 更新总项目数量并重新计算分页
   * @param {number} newTotalItems - 新的总项目数量
   */
  updateTotalItems(newTotalItems) {
    this.totalItems = newTotalItems;
    this.totalPages = Math.max(1, Math.ceil(this.totalItems / this.pageSize));
    
    // 如果当前页码超出新的总页数，跳转到最后一页
    if (this.currentPage > this.totalPages) {
      this.currentPage = this.totalPages;
    }
    
    // 重新获取项目元素
    this.items = document.querySelectorAll(this.itemsSelector);
    
    // 重新初始化
    this.init();
  }
  
  /**
   * 销毁分页组件，清理事件监听器
   */
  destroy() {
    if (this.container) {
      this.container.innerHTML = '';
      // 移除事件监听器
      this.container.removeEventListener('click', this.bindEvents);
    }
  }
  
  /**
   * 获取当前分页状态信息
   * @returns {Object} - 包含当前页码、总页数、总项目数等信息的对象
   */
  getStatus() {
    return {
      currentPage: this.currentPage,
      totalPages: this.totalPages,
      totalItems: this.totalItems,
      pageSize: this.pageSize,
      startIndex: (this.currentPage - 1) * this.pageSize,
      endIndex: Math.min(this.currentPage * this.pageSize - 1, this.totalItems - 1)
    };
  }
}