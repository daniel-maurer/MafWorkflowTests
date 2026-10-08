let tokenProvider: (() => Promise<string | null>) | null = null;

export function setAdminTokenProvider(provider: () => Promise<string | null>) {
  tokenProvider = provider;
}

const ADMIN_API_BASE = (import.meta.env.VITE_ADMIN_API_BASE_URL as string) || 'http://localhost:5100/api';

async function request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const token = tokenProvider ? await tokenProvider() : null;
  const headers = new Headers(options.headers || {});
  headers.set('Content-Type', 'application/json');

  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  } else {
    headers.set('Authorization', 'Bearer mock-token:admin-user');
  }

  const response = await fetch(`${ADMIN_API_BASE}${endpoint}`, {
    ...options,
    headers,
  });

  if (!response.ok) {
    let errorMsg = `Erro na requisição: ${response.status} ${response.statusText}`;
    try {
      const errJson = await response.json();
      if (errJson.error) errorMsg = errJson.error;
    } catch {
      // ignore
    }
    throw new Error(errorMsg);
  }

  if (response.status === 204) {
    return {} as T;
  }

  return response.json();
}

export interface ProductItem {
  id: string;
  sku: string;
  name: string;
  description: string;
  price: number;
  currency: string;
  inStock: boolean;
  stockQty: number;
  color?: string;
  size?: string;
  brand?: string;
  categoryId?: string;
  categoryName?: string;
  tags: string[];
  compatibleSkus: string[];
  imageUrl?: string;
  active: boolean;
  hasEmbedding?: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CategoryItem {
  id: string;
  name: string;
  description?: string;
  slug: string;
  active: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface DiscountItem {
  id: string;
  code: string;
  name: string;
  description?: string;
  discountType: 'percentage' | 'fixed_amount';
  discountValue: number;
  minOrderValue?: number;
  maxDiscountAmount?: number;
  validFrom: string;
  validUntil?: string;
  paymentMethods: string[];
  maxUses?: number;
  currentUses: number;
  active: boolean;
  stackable: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CustomerAddressItem {
  id?: string;
  label: string;
  street: string;
  number: string;
  complement?: string;
  neighborhood: string;
  city: string;
  state: string;
  zipCode: string;
  country?: string;
  isDefault: boolean;
}

export interface CustomerItem {
  id: string;
  name: string;
  email: string;
  phone?: string;
  documentNumber?: string;
  documentType?: string;
  customerType: 'individual' | 'business';
  notes?: string;
  active: boolean;
  createdAt: string;
  updatedAt: string;
  addresses: CustomerAddressItem[];
}

export interface PaymentConditionItem {
  id: string;
  name: string;
  paymentMethod: 'pix' | 'credit_card' | 'debit_card' | 'boleto';
  maxInstallments: number;
  interestFree: boolean;
  interestRate: number;
  additionalDiscount: number;
  active: boolean;
  createdAt: string;
  updatedAt: string;
}

export const adminApi = {
  // Produtos
  listProducts: (params?: { search?: string; categoryId?: string; brand?: string; active?: boolean; page?: number; pageSize?: number }) => {
    const q = new URLSearchParams();
    if (params?.search) q.set('search', params.search);
    if (params?.categoryId) q.set('categoryId', params.categoryId);
    if (params?.brand) q.set('brand', params.brand);
    if (params?.active !== undefined) q.set('active', String(params.active));
    if (params?.page) q.set('page', String(params.page));
    if (params?.pageSize) q.set('pageSize', String(params.pageSize));
    return request<{ total: number; page: number; pageSize: number; items: ProductItem[] }>(`/products?${q.toString()}`);
  },
  getProduct: (id: string) => request<ProductItem>(`/products/${id}`),
  createProduct: (data: Partial<ProductItem>) => request<ProductItem>('/products', { method: 'POST', body: JSON.stringify(data) }),
  updateProduct: (id: string, data: Partial<ProductItem>) => request<ProductItem>(`/products/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  deleteProduct: (id: string) => request<void>(`/products/${id}`, { method: 'DELETE' }),
  reindexEmbeddings: () => request<{ message: string }>('/products/reindex-embeddings', { method: 'POST' }),

  // Categorias
  listCategories: (active?: boolean) => {
    const q = active !== undefined ? `?active=${active}` : '';
    return request<CategoryItem[]>(`/categories${q}`);
  },
  createCategory: (data: Partial<CategoryItem>) => request<CategoryItem>('/categories', { method: 'POST', body: JSON.stringify(data) }),
  updateCategory: (id: string, data: Partial<CategoryItem>) => request<CategoryItem>(`/categories/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  deleteCategory: (id: string) => request<void>(`/categories/${id}`, { method: 'DELETE' }),

  // Descontos
  listDiscounts: (params?: { active?: boolean; validNow?: boolean; paymentMethod?: string }) => {
    const q = new URLSearchParams();
    if (params?.active !== undefined) q.set('active', String(params.active));
    if (params?.validNow !== undefined) q.set('validNow', String(params.validNow));
    if (params?.paymentMethod) q.set('paymentMethod', params.paymentMethod);
    return request<DiscountItem[]>(`/discounts?${q.toString()}`);
  },
  getDiscount: (id: string) => request<DiscountItem>(`/discounts/${id}`),
  createDiscount: (data: Partial<DiscountItem>) => request<DiscountItem>('/discounts', { method: 'POST', body: JSON.stringify(data) }),
  updateDiscount: (id: string, data: Partial<DiscountItem>) => request<DiscountItem>(`/discounts/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  deleteDiscount: (id: string) => request<void>(`/discounts/${id}`, { method: 'DELETE' }),

  // Clientes
  listCustomers: (params?: { search?: string; active?: boolean; page?: number; pageSize?: number }) => {
    const q = new URLSearchParams();
    if (params?.search) q.set('search', params.search);
    if (params?.active !== undefined) q.set('active', String(params.active));
    if (params?.page) q.set('page', String(params.page));
    if (params?.pageSize) q.set('pageSize', String(params.pageSize));
    return request<{ total: number; page: number; pageSize: number; items: CustomerItem[] }>(`/customers?${q.toString()}`);
  },
  getCustomer: (id: string) => request<CustomerItem>(`/customers/${id}`),
  createCustomer: (data: Partial<CustomerItem>) => request<CustomerItem>('/customers', { method: 'POST', body: JSON.stringify(data) }),
  updateCustomer: (id: string, data: Partial<CustomerItem>) => request<CustomerItem>(`/customers/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  deleteCustomer: (id: string) => request<void>(`/customers/${id}`, { method: 'DELETE' }),
  addCustomerAddress: (customerId: string, address: Partial<CustomerAddressItem>) =>
    request<CustomerAddressItem>(`/customers/${customerId}/addresses`, { method: 'POST', body: JSON.stringify(address) }),
  deleteCustomerAddress: (customerId: string, addressId: string) =>
    request<void>(`/customers/${customerId}/addresses/${addressId}`, { method: 'DELETE' }),

  // Condições de Pagamento
  listPaymentConditions: (active?: boolean) => {
    const q = active !== undefined ? `?active=${active}` : '';
    return request<PaymentConditionItem[]>(`/payment-conditions${q}`);
  },
  createPaymentCondition: (data: Partial<PaymentConditionItem>) =>
    request<PaymentConditionItem>('/payment-conditions', { method: 'POST', body: JSON.stringify(data) }),
  updatePaymentCondition: (id: string, data: Partial<PaymentConditionItem>) =>
    request<PaymentConditionItem>(`/payment-conditions/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  deletePaymentCondition: (id: string) => request<void>(`/payment-conditions/${id}`, { method: 'DELETE' }),

  // Follow-Ups (Compartilhado)
  listFollowUps: (params?: { customerId?: string; module?: string; status?: string; search?: string; page?: number; pageSize?: number }) => {
    const q = new URLSearchParams();
    if (params?.customerId) q.set('customerId', params.customerId);
    if (params?.module) q.set('module', params.module);
    if (params?.status) q.set('status', params.status);
    if (params?.search) q.set('search', params.search);
    if (params?.page) q.set('page', String(params.page));
    if (params?.pageSize) q.set('pageSize', String(params.pageSize));
    return request<{ total: number; page: number; pageSize: number; items: FollowUpItem[] }>(`/follow-ups?${q.toString()}`);
  },
  getFollowUp: (id: string) => request<FollowUpItem>(`/follow-ups/${id}`),
  createFollowUp: (data: Partial<FollowUpItem>) => request<FollowUpItem>('/follow-ups', { method: 'POST', body: JSON.stringify(data) }),
  updateFollowUpStatus: (id: string, status: string, notes?: string) =>
    request<{ message: string; status: string; sentAt?: string }>(`/follow-ups/${id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status, notes }),
    }),
  updateFollowUp: (id: string, data: Partial<FollowUpItem>) =>
    request<FollowUpItem>(`/follow-ups/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  deleteFollowUp: (id: string) => request<void>(`/follow-ups/${id}`, { method: 'DELETE' }),
  getCustomerFollowUps: (customerId: string) => request<FollowUpItem[]>(`/customers/${customerId}/follow-ups`),

  // Agent Instructions (Compartilhado & Workflow)
  listAgentInstructions: (workflowType?: string) => {
    const q = workflowType ? `?workflowType=${encodeURIComponent(workflowType)}` : '';
    return request<AgentInstructionItem[]>(`/agent-instructions${q}`);
  },
  createAgentInstruction: (data: Partial<AgentInstructionItem>) =>
    request<AgentInstructionItem>('/agent-instructions', { method: 'POST', body: JSON.stringify(data) }),
  updateAgentInstruction: (id: string, data: Partial<AgentInstructionItem>) =>
    request<void>(`/agent-instructions/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  deleteAgentInstruction: (id: string) =>
    request<void>(`/agent-instructions/${id}`, { method: 'DELETE' }),

  // Delivery Methods
  listDeliveryMethods: () => request<DeliveryMethodItem[]>('/delivery-methods'),
  createDeliveryMethod: (data: Partial<DeliveryMethodItem>) => request<DeliveryMethodItem>('/delivery-methods', { method: 'POST', body: JSON.stringify(data) }),
  updateDeliveryMethod: (id: string, data: Partial<DeliveryMethodItem>) => request<void>(`/delivery-methods/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  deleteDeliveryMethod: (id: string) => request<void>(`/delivery-methods/${id}`, { method: 'DELETE' }),

  // Store Info
  getStoreInfo: () => request<StoreInfoItem>('/store-info'),
  createStoreInfo: (data: Partial<StoreInfoItem>) => request<StoreInfoItem>('/store-info', { method: 'POST', body: JSON.stringify(data) }),
  updateStoreInfo: (id: string, data: Partial<StoreInfoItem>) => request<void>(`/store-info/${id}`, { method: 'PUT', body: JSON.stringify(data) }),

  // Orders
  listOrders: () => request<OrderItem[]>('/orders'),
  getOrder: (id: string) => request<OrderItem>(`/orders/${id}`),
  deleteOrder: (id: string) => request<void>(`/orders/${id}`, { method: 'DELETE' }),

  // Conversations
  listConversations: () => request<ConversationItem[]>('/conversations'),
  getConversation: (id: string) => request<ConversationItem>(`/conversations/${id}`),
  deleteConversation: (id: string) => request<void>(`/conversations/${id}`, { method: 'DELETE' }),
  // Campaigns
  listCampaigns: (params?: { active?: boolean }) => {
    const q = params?.active !== undefined ? `?active=${params.active}` : '';
    return request<CampaignItem[]>(`/campaigns${q}`);
  },
  getCampaign: (id: string) => request<CampaignItem>(`/campaigns/${id}`),
  createCampaign: (data: Partial<CampaignItem>) => request<CampaignItem>('/campaigns', { method: 'POST', body: JSON.stringify(data) }),
  updateCampaign: (id: string, data: Partial<CampaignItem>) => request<CampaignItem>(`/campaigns/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  deleteCampaign: (id: string) => request<void>(`/campaigns/${id}`, { method: 'DELETE' }),

};

export interface CampaignItem {
  id?: string;
  name: string;
  description: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
  freeShipping: boolean;
  globalDiscountPercent: number;
  discount1Item: number;
  discount2Items: number;
  discount3PlusItems: number;
  customRulesJson?: string;
}

export interface DeliveryMethodItem {
  id: string;
  name: string;
  type: string;
  description?: string;
  price: number;
  active: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface StoreInfoItem {
  id: string;
  name: string;
  address: string;
  phone?: string;
  email?: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface OrderItem {
  id: string;
  customerId: string;
  status: string;
  totalAmount: number;
  deliveryMethod?: string;
  paymentMethod?: string;
  createdAt: string;
  items?: {
    id: string;
    sku: string;
    name: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
  }[];
}

export interface ConversationItem {
  id: string;
  customerId?: string;
  sessionId: string;
  status: string;
  createdAt: string;
  messages?: {
    id: string;
    role: string;
    content: string;
    createdAt: string;
  }[];
}

export interface AgentInstructionItem {
  id?: string;
  workflowType: string;
  agentRole: string;
  instructions: string;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface FollowUpItem {
  id: string;
  customerId: string;
  customerName: string;
  customerEmail: string;
  customerPhone?: string | null;
  module: string; // 'sales' | 'vet' | 'support'
  followUpType: string;
  referenceId?: string | null;
  referenceTitle?: string | null;
  scheduledFor: string;
  channel: string;
  messageText: string;
  status: string; // 'Pending' | 'Sent' | 'Cancelled' | 'Completed'
  sentAt?: string | null;
  notes?: string | null;
  customDataJson?: string | null;
  createdAt: string;
}
